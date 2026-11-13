using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Application.Contracts.Features.PaymentContracts.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.Payment.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;

using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Application.Contracts.Features.PaymentContracts.Queries;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    public class PaymentAppService : WashGoAppService, IPaymentAppService
    {
        private readonly IWashOrderAppService _orderService;
        private readonly IPaymentTransactionRepository _transactionRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRequestRepo;
        private readonly IConfiguration _configuration;
        private readonly IWashOrderRepository _orders;
        private readonly NotificationPublisher _notifications;
        private readonly SystemSettingProvider _settings;
        private readonly MomoGateway _momo;

        public PaymentAppService(
            IWashOrderAppService orderService,
            IPaymentTransactionRepository transactionRepo,
            IConfiguration configuration,
            IWashOrderRepository orders,
            IRepository<RefundRequest, Guid> refundRequestRepo,
            NotificationPublisher notifications,
            SystemSettingProvider settings,
            MomoGateway momo)
        {
            _orderService = orderService;
            _transactionRepo = transactionRepo;
            _configuration = configuration;
            _orders = orders;
            _refundRequestRepo = refundRequestRepo;
            _notifications = notifications;
            _settings = settings;
            _momo = momo;
        }

        public async Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command)
        {
            var orderResponse = await _orderService.GetByIdAsync(command.OrderId);
            if (!orderResponse.Succeeded || orderResponse.Data == null)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần thanh toán.");

            var order = orderResponse.Data;
            if (order.PaymentStatus == PaymentStatus.Paid)
                return BadRequest<PaymentResultDto>("Đơn hàng đã được thanh toán.");

            // UC 1.2.4 7b: an order moved to storage for non-payment can still be paid, then picked up at the storage.
            if (order.Status is not (WashOrderStatus.AwaitingPayment or WashOrderStatus.MovedToStorage))
                return BadRequest<PaymentResultDto>("Đơn hàng chưa ở trạng thái chờ thanh toán.");
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
                return BadRequest<PaymentResultDto>("Đơn chưa được Merchant cân thực tế và chốt giá.");

            if (command.PaymentMethod == PaymentMethod.Cash)
                return BadRequest<PaymentResultDto>("Payment URL chỉ áp dụng cho phương thức thanh toán trực tuyến.");

            var transactionReference = NewReference("PAY", order.OrderCode, command.PaymentMethod);
            var (paymentUrl, gatewayError) = await GatewayUrlAsync(order.Id, command.PaymentMethod, transactionReference,
                order.OrderCode, order.FinalAmount, command.ClientIpAddress);
            if (paymentUrl == null)
                return BadRequest<PaymentResultDto>(gatewayError!);

            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
                MerchantIdSnapshot = order.MerchantId, CommissionRateSnapshot = order.CommissionRateApplied, FinancialSnapshotBasis = "payment-created",
                OrderId = order.Id,
                TransactionReference = transactionReference,
                PaymentMethod = command.PaymentMethod,
                Purpose = PaymentPurpose.Order,
                Status = PaymentTransactionStatus.Pending,
                Amount = order.FinalAmount,
                PaymentUrl = paymentUrl
            }, autoSave: true);

            return Success(new PaymentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Amount = order.FinalAmount,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransactionReference = transactionReference,
                PaymentUrl = paymentUrl
            }, UseLiveGateway ? $"Tạo URL thanh toán {GatewayName(command.PaymentMethod)} thành công." : "Tạo payment URL sandbox thành công.");
        }

        public async Task<BaseResponse<PaymentResultDto>> CreateOverdueFeePaymentAsync(CreateOverdueFeePaymentCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<PaymentResultDto>("Không xác định được tài khoản khách hàng.");
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status == WashOrderStatus.MovedToStorage && order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<PaymentResultDto>("Vui lòng thanh toán tiền giặt trước khi thanh toán phí lưu kho.");
            if (order.PaymentStatus != PaymentStatus.Paid ||
                order.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage or WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage))
                return BadRequest<PaymentResultDto>("Phí lưu kho chỉ thanh toán khi đồ đã được bỏ vào locker chờ nhận.");

            var overdueTotal = OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow, await _settings.GetAsync());
            order.OverdueFeeTotal = overdueTotal;
            var outstanding = overdueTotal - order.StorageFeeTotal;
            if (outstanding <= 0m)
            {
                await _orders.UpdateAsync(order, autoSave: true);
                await uow.CompleteAsync();
                return BadRequest<PaymentResultDto>("Đơn hàng hiện không có phí lưu kho cần thanh toán.");
            }

            var pending = await _transactionRepo.FirstOrDefaultAsync(x =>
                x.OrderId == order.Id && x.Purpose == PaymentPurpose.OverdueStorageFee &&
                x.Status == PaymentTransactionStatus.Pending);
            if (pending != null)
                return BadRequest<PaymentResultDto>("Đơn đã có giao dịch phí lưu kho đang chờ xác nhận. Vui lòng chờ kết quả trước khi tạo giao dịch mới.");

            var reference = NewReference("FEE", order.OrderCode, command.PaymentMethod);
            var (paymentUrl, gatewayError) = await GatewayUrlAsync(order.Id, command.PaymentMethod, reference,
                order.OrderCode, outstanding, command.ClientIpAddress, $"Overdue storage fee {order.OrderCode}");
            if (paymentUrl == null)
                return BadRequest<PaymentResultDto>(gatewayError!);

            await _orders.UpdateAsync(order, autoSave: true);
            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
                MerchantIdSnapshot = order.MerchantId, CommissionRateSnapshot = order.CommissionRateApplied, FinancialSnapshotBasis = "payment-created",
                OrderId = order.Id,
                TransactionReference = reference,
                PaymentMethod = command.PaymentMethod,
                Purpose = PaymentPurpose.OverdueStorageFee,
                Status = PaymentTransactionStatus.Pending,
                Amount = outstanding,
                PaymentUrl = paymentUrl
            }, autoSave: true);
            await uow.CompleteAsync();

            return Success(new PaymentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Amount = outstanding,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransactionReference = reference,
                PaymentUrl = paymentUrl
            }, "Đã tạo giao dịch thanh toán phí lưu kho.");
        }

        public async Task<BaseResponse<PaymentResultDto>> CreateCancellationFeePaymentAsync(CreatePaymentCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<PaymentResultDto>("Không xác định được tài khoản khách hàng.");
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status != WashOrderStatus.Cancelled)
                return BadRequest<PaymentResultDto>("Chỉ thanh toán phí hủy cho đơn đã hủy.");

            var overdueTotal = order.CancelFeeAmount;

            var outstanding = overdueTotal - order.CancelFeePaidAmount;
            if (outstanding <= 0m)
            {
                await _orders.UpdateAsync(order, autoSave: true);
                await uow.CompleteAsync();
                return BadRequest<PaymentResultDto>("Đơn hàng hiện không có phí hủy cần thanh toán.");
            }

            var pending = await _transactionRepo.FirstOrDefaultAsync(x =>
                x.OrderId == order.Id && x.Purpose == PaymentPurpose.CancellationFee &&
                x.Status == PaymentTransactionStatus.Pending);
            if (pending != null)
                return BadRequest<PaymentResultDto>("Đơn đã có giao dịch phí hủy đang chờ xác nhận. Vui lòng chờ kết quả trước khi tạo giao dịch mới.");

            var reference = NewReference("CANCEL", order.OrderCode, command.PaymentMethod);
            var (paymentUrl, gatewayError) = await GatewayUrlAsync(order.Id, command.PaymentMethod, reference,
                order.OrderCode, outstanding, command.ClientIpAddress, $"Cancellation fee {order.OrderCode}");
            if (paymentUrl == null)
                return BadRequest<PaymentResultDto>(gatewayError!);

            await _orders.UpdateAsync(order, autoSave: true);
            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
                MerchantIdSnapshot = order.MerchantId, CommissionRateSnapshot = order.CommissionRateApplied, FinancialSnapshotBasis = "payment-created",
                OrderId = order.Id,
                TransactionReference = reference,
                PaymentMethod = command.PaymentMethod,
                Purpose = PaymentPurpose.CancellationFee,
                Status = PaymentTransactionStatus.Pending,
                Amount = outstanding,
                PaymentUrl = paymentUrl
            }, autoSave: true);
            await uow.CompleteAsync();

            return Success(new PaymentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Amount = outstanding,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                Purpose = PaymentPurpose.CancellationFee,
                TransactionReference = reference,
                PaymentUrl = paymentUrl
            }, "Đã tạo giao dịch thanh toán phí hủy.");
        }

        public async Task<BaseResponse<PaymentResultDto>> HandleCallbackAsync(PaymentCallbackCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.TransactionReference))
                return BadRequest<PaymentResultDto>("Thiếu mã giao dịch thanh toán.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var transaction = await _transactionRepo.GetForUpdateByReferenceAsync(command.TransactionReference);
            if (transaction == null || transaction.OrderId != command.OrderId)
                return BadRequest<PaymentResultDto>("Mã giao dịch không hợp lệ.");
            if (transaction.PaymentMethod is not (PaymentMethod.VnPay or PaymentMethod.Momo) || UseLiveGateway)
                return BadRequest<PaymentResultDto>("Callback sandbox không được phép xác nhận giao dịch của cổng thanh toán thật.");

            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần cập nhật thanh toán.");
            if (!command.Success)
            {
                if (transaction.Status != PaymentTransactionStatus.Pending)
                    return BadRequest<PaymentResultDto>("Giao dịch đã được xử lý trước đó.");
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureReason = "Sandbox payment failed or was cancelled.";
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                await _notifications.PaymentFailedAsync(order);
                await uow.CompleteAsync();
                return BadRequest<PaymentResultDto>("Thanh toán thất bại hoặc bị hủy.");
            }

            if (transaction.Purpose == PaymentPurpose.CancellationFee)
            {
                if (transaction.Status != PaymentTransactionStatus.Succeeded)
                {
                    if (!await MarkCancellationFeePaidAsync(order, transaction, "Sandbox"))
                        return BadRequest<PaymentResultDto>("Giao dịch hoặc số phí hủy không hợp lệ.");
                    transaction.Status = PaymentTransactionStatus.Succeeded;
                    transaction.CompletedAt = DateTime.UtcNow;
                    await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                    await uow.CompleteAsync();
                }
                return Success(new PaymentResultDto
                {
                    OrderId = order.Id, OrderCode = order.OrderCode, Amount = transaction.Amount,
                    PaymentMethod = transaction.PaymentMethod, PaymentStatus = order.PaymentStatus,
                    TransactionReference = transaction.TransactionReference, Purpose = PaymentPurpose.CancellationFee
                }, "Đã ghi nhận thanh toán phí hủy; đơn hàng vẫn ở trạng thái Đã hủy.");
            }
            if (transaction.Purpose == PaymentPurpose.OverdueStorageFee)
            {
                if (transaction.Status == PaymentTransactionStatus.Succeeded)
                    return Success(new PaymentResultDto
                    {
                        OrderId = order.Id, OrderCode = order.OrderCode, Amount = transaction.Amount,
                        PaymentMethod = transaction.PaymentMethod, PaymentStatus = order.PaymentStatus,
                        TransactionReference = transaction.TransactionReference
                    }, "Phí lưu kho đã được ghi nhận trước đó.");
                if (transaction.Status != PaymentTransactionStatus.Pending)
                    return BadRequest<PaymentResultDto>("Giao dịch đã được xử lý trước đó.");

                var orderEntity = await _orders.GetForUpdateAsync(transaction.OrderId);
                if (orderEntity == null)
                    return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần cập nhật phí.");
                if (orderEntity.PaymentStatus != PaymentStatus.Paid ||
                    orderEntity.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage or WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage))
                    return BadRequest<PaymentResultDto>("Đơn không còn chờ khách nhận tại locker.");

                var due = OverdueFeeCalculator.GetTotalStorageDue(orderEntity, DateTime.UtcNow, await _settings.GetAsync());
                if (transaction.Amount > due - orderEntity.StorageFeeTotal)
                    return BadRequest<PaymentResultDto>("Giao dịch phí lưu kho lớn hơn số tiền còn phải trả.");

                orderEntity.OverdueFeeTotal = due;
                orderEntity.StorageFeeTotal += transaction.Amount;
                await _orders.UpdateAsync(orderEntity, autoSave: true);
                transaction.Status = PaymentTransactionStatus.Succeeded;
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                await uow.CompleteAsync();
                return Success(new PaymentResultDto
                {
                    OrderId = order.Id, OrderCode = order.OrderCode, Amount = transaction.Amount,
                    PaymentMethod = transaction.PaymentMethod, PaymentStatus = order.PaymentStatus,
                    TransactionReference = transaction.TransactionReference
                }, "Thanh toán phí lưu kho thành công.");
            }

            if (transaction.Purpose != PaymentPurpose.Order)
                return BadRequest<PaymentResultDto>("Mục đích giao dịch không hợp lệ.");
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                if (transaction.Status != PaymentTransactionStatus.Succeeded)
                    return BadRequest<PaymentResultDto>("Đơn đã được thanh toán bởi giao dịch khác.");

                return Success(new PaymentResultDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Amount = order.FinalAmount,
                    PaymentMethod = transaction.PaymentMethod,
                    PaymentStatus = order.PaymentStatus,
                    TransactionReference = command.TransactionReference
                }, "Đơn hàng đã được ghi nhận thanh toán trước đó.");
            }

            if (!await MarkOrderPaidAsync(order, transaction, "WashGo Payment Sandbox"))
                return BadRequest<PaymentResultDto>("Đơn hoặc số tiền không còn hợp lệ để xác nhận thanh toán.");

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);
            await uow.CompleteAsync();

            return Success(new PaymentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Amount = order.FinalAmount,
                PaymentMethod = transaction.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransactionReference = command.TransactionReference
            }, "Thanh toán thành công.");
        }

        public async Task<VnPayIpnResultDto> HandleVnPayIpnAsync(IReadOnlyDictionary<string, string> parameters)
        {
            var secret = _configuration["Payment:VnPay:HashSecret"];
            if (string.IsNullOrWhiteSpace(secret) || !VerifyVnPaySignature(parameters, secret))
                return Ip("97", "Invalid signature");

            if (!parameters.TryGetValue("vnp_TmnCode", out var tmnCode) ||
                !string.Equals(tmnCode, _configuration["Payment:VnPay:TmnCode"], StringComparison.Ordinal))
                return Ip("97", "Invalid merchant");

            if (!parameters.TryGetValue("vnp_TxnRef", out var reference) || string.IsNullOrWhiteSpace(reference))
                return Ip("01", "Order not found");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var transaction = await _transactionRepo.GetForUpdateByReferenceAsync(reference);
            if (transaction == null || transaction.PaymentMethod != PaymentMethod.VnPay)
                return Ip("01", "Order not found");
            if (transaction.Status == PaymentTransactionStatus.Succeeded)
                return Ip("02", "Order already confirmed");
            if (transaction.Status != PaymentTransactionStatus.Pending)
                return Ip("02", "Order already processed");

            if (!parameters.TryGetValue("vnp_Amount", out var amountText) ||
                !long.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
                amount != decimal.ToInt64(decimal.Round(transaction.Amount * 100m, 0, MidpointRounding.AwayFromZero)))
                return Ip("04", "Invalid amount");

            var responseCode = parameters.GetValueOrDefault("vnp_ResponseCode", "");
            var transactionStatus = parameters.GetValueOrDefault("vnp_TransactionStatus", "");
            var outcome = await ApplyGatewayResultAsync(transaction, responseCode == "00" && transactionStatus == "00",
                parameters.GetValueOrDefault("vnp_TransactionNo"),
                $"VNPay response={responseCode}, status={transactionStatus}", "VNPay");
            if (outcome != GatewayOutcome.Confirmed)
                return outcome switch
                {
                    GatewayOutcome.NotFound => Ip("01", "Order not found"),
                    GatewayOutcome.AlreadyPaid => Ip("02", "Order already confirmed"),
                    GatewayOutcome.InvalidAmount => Ip("04", "Invalid amount"),
                    _ => Ip("04", "Invalid order state or amount")
                };
            await uow.CompleteAsync();
            return Ip("00", "Confirm Success");
        }

        public async Task<bool> HandleMomoIpnAsync(MomoIpnCommand ipn)
        {
            if (!_momo.VerifyIpn(ipn))
            {
                Logger.LogWarning("MoMo IPN {OrderId} rejected: invalid signature or partner.", ipn.OrderId);
                return false;
            }

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var transaction = await _transactionRepo.GetForUpdateByReferenceAsync(ipn.OrderId);
            // MoMo retries until it gets 204, so already-handled or unknown IPNs are acknowledged without changes.
            if (transaction == null || transaction.PaymentMethod != PaymentMethod.Momo ||
                transaction.Status != PaymentTransactionStatus.Pending)
            {
                Logger.LogInformation("MoMo IPN {OrderId} ignored: unknown or already processed.", ipn.OrderId);
                return true;
            }
            if (ipn.Amount != decimal.ToInt64(decimal.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero)))
            {
                // UC 1.2.4 exception 7a: an invalid gateway result is logged and the order is not updated.
                Logger.LogError("MoMo IPN {OrderId} amount {Amount} does not match {Expected}.", ipn.OrderId, ipn.Amount, transaction.Amount);
                return true;
            }

            var outcome = await ApplyGatewayResultAsync(transaction, ipn.ResultCode == 0,
                ipn.TransId.ToString(CultureInfo.InvariantCulture), $"MoMo resultCode={ipn.ResultCode}: {ipn.Message}", "MoMo");
            if (outcome == GatewayOutcome.Confirmed)
                await uow.CompleteAsync();
            else
                Logger.LogError("MoMo IPN {OrderId} not applied: {Outcome}.", ipn.OrderId, outcome);
            return true;
        }

        private enum GatewayOutcome { Confirmed, NotFound, AlreadyPaid, InvalidAmount, InvalidState }

        /// <summary>
        /// Applies a verified gateway result (VNPay or MoMo IPN) to a pending transaction. The caller owns the
        /// unit of work and commits only when the outcome is Confirmed.
        /// </summary>
        private async Task<GatewayOutcome> ApplyGatewayResultAsync(PaymentTransaction transaction, bool success,
            string? providerTransactionId, string failureReason, string provider)
        {
            if (!success)
            {
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureReason = failureReason;
                transaction.ProviderTransactionId = providerTransactionId;
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                if (await _orders.FindAsync(transaction.OrderId) is { } failedOrder)
                    await _notifications.PaymentFailedAsync(failedOrder);
                return GatewayOutcome.Confirmed;
            }

            var order = await _orders.GetForUpdateAsync(transaction.OrderId);
            if (order == null)
                return GatewayOutcome.NotFound;

            switch (transaction.Purpose)
            {
                case PaymentPurpose.CancellationFee:
                    if (!await MarkCancellationFeePaidAsync(order, transaction, provider))
                        return GatewayOutcome.InvalidAmount;
                    break;
                case PaymentPurpose.OverdueStorageFee:
                    if (order.PaymentStatus != PaymentStatus.Paid ||
                        order.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage or WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage))
                        return GatewayOutcome.InvalidState;
                    var due = OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow, await _settings.GetAsync());
                    if (transaction.Amount > due - order.StorageFeeTotal)
                        return GatewayOutcome.InvalidAmount;
                    order.OverdueFeeTotal = due;
                    order.StorageFeeTotal += transaction.Amount;
                    await _orders.UpdateAsync(order, autoSave: true);
                    break;
                case PaymentPurpose.Order:
                    if (order.PaymentStatus == PaymentStatus.Paid)
                        return GatewayOutcome.AlreadyPaid;
                    if (!await MarkOrderPaidAsync(order, transaction, provider, providerTransactionId))
                        return GatewayOutcome.InvalidState;
                    break;
                default:
                    return GatewayOutcome.InvalidState;
            }

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.ProviderTransactionId = providerTransactionId;
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);
            return GatewayOutcome.Confirmed;
        }

        private async Task<bool> MarkCancellationFeePaidAsync(WashOrder order, PaymentTransaction transaction, string provider)
        {
            var outstanding = order.CancelFeeAmount - order.CancelFeePaidAmount;
            if (order.Status != WashOrderStatus.Cancelled || outstanding <= 0m ||
                transaction.OrderId != order.Id || transaction.Purpose != PaymentPurpose.CancellationFee ||
                transaction.Status != PaymentTransactionStatus.Pending || transaction.Amount != outstanding)
                return false;
            order.CancelFeePaidAmount += transaction.Amount;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id, FromStatus = order.Status, Status = order.Status,
                ActorRole = "System", ActorName = provider,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Thanh toán phí hủy {transaction.Amount:N0}đ. Mã: {transaction.TransactionReference}"
            });
            await _orders.UpdateAsync(order, autoSave: true);
            return true;
        }
        private async Task<bool> MarkOrderPaidAsync(WashOrder order, PaymentTransaction transaction,
            string provider, string? providerReference = null)
        {
            if (transaction.OrderId != order.Id || transaction.Purpose != PaymentPurpose.Order ||
                transaction.Status != PaymentTransactionStatus.Pending ||
                transaction.Amount != order.FinalAmount ||
                order.Status is not (WashOrderStatus.AwaitingPayment or WashOrderStatus.MovedToStorage) ||
                order.PaymentStatus != PaymentStatus.Unpaid ||
                order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
                return false;

            // UC 1.2.4 7b: laundry paid after the move to storage stays in storage until the customer picks it up.
            var fromStatus = order.Status;
            if (fromStatus == WashOrderStatus.AwaitingPayment)
                order.Status = WashOrderStatus.Paid;
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = fromStatus,
                Status = order.Status,
                ActorName = provider,
                ActorRole = "PaymentGateway",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Thanh toán thành công, mã giao dịch: {providerReference ?? transaction.TransactionReference}"
            });
            await _orders.UpdateAsync(order, autoSave: true);
            await _notifications.PaymentSucceededAsync(order);
            return true;
        }

        /// <summary>Payment:Provider = Mock (default, local mock checkout) or Live (real VNPay/MoMo sandbox); "VnPay" is the old name of Live.</summary>
        private bool UseLiveGateway =>
            _configuration["Payment:Provider"] is { Length: > 0 } provider &&
            !string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase);

        private static string GatewayName(PaymentMethod method) => method == PaymentMethod.Momo ? "MoMo" : "VNPay";

        // MoMo limits orderId to 50 characters, so its references use a 16-character random suffix.
        private static string NewReference(string kind, string orderCode, PaymentMethod method) =>
            method == PaymentMethod.Momo
                ? $"WG-{kind}-{orderCode}-{Guid.NewGuid():N}"[..(kind.Length + orderCode.Length + 21)]
                : $"WG-{kind}-{orderCode}-{Guid.NewGuid():N}";

        private async Task<(string? Url, string? Error)> GatewayUrlAsync(Guid orderId, PaymentMethod method, string reference,
            string orderCode, decimal amount, string clientIpAddress, string orderInfo = "")
        {
            if (method is not (PaymentMethod.VnPay or PaymentMethod.Momo))
                return (null, "Hiện chỉ hỗ trợ thanh toán trực tuyến qua VNPay hoặc MoMo.");
            if (!UseLiveGateway)
                return ($"http://localhost:5000/payment/mock-checkout?orderId={orderId}&reference={reference}", null);
            if (method == PaymentMethod.Momo)
                return await _momo.CreatePayUrlAsync(reference, amount,
                    string.IsNullOrWhiteSpace(orderInfo) ? $"Thanh toan don WashGo {orderCode}" : orderInfo);
            var url = BuildVnPayUrl(orderCode, reference, amount, clientIpAddress, orderInfo);
            return url == null ? (null, "Chưa cấu hình đầy đủ thông tin kết nối VNPay.") : (url, null);
        }

        private string? BuildVnPayUrl(string orderCode, string reference, decimal amount, string clientIpAddress,
            string orderInfo = "")
        {
            var tmnCode = _configuration["Payment:VnPay:TmnCode"];
            var secret = _configuration["Payment:VnPay:HashSecret"];
            var returnUrl = _configuration["Payment:VnPay:ReturnUrl"];
            if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(returnUrl))
                return null;

            var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
            var ip = string.IsNullOrWhiteSpace(clientIpAddress) ? "127.0.0.1" : clientIpAddress;
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode,
                ["vnp_Amount"] = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture),
                ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                ["vnp_CurrCode"] = "VND",
                ["vnp_IpAddr"] = ip,
                ["vnp_Locale"] = "vn",
                ["vnp_OrderInfo"] = string.IsNullOrWhiteSpace(orderInfo) ? $"Thanh toan don WashGo {orderCode}" : orderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_TxnRef"] = reference,
                ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            };
            var query = string.Join("&", fields.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
            var hash = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(query))).ToLowerInvariant();
            var baseUrl = _configuration["Payment:VnPay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            return $"{baseUrl}?{query}&vnp_SecureHash={hash}";
        }

        private static bool VerifyVnPaySignature(IReadOnlyDictionary<string, string> parameters, string secret)
        {
            if (!parameters.TryGetValue("vnp_SecureHash", out var suppliedHash) || suppliedHash.Length != 128)
                return false;
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in parameters)
                if (pair.Key.StartsWith("vnp_", StringComparison.Ordinal) && pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
                    fields[pair.Key] = pair.Value;
            var query = string.Join("&", fields.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
            var expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(query));
            try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(suppliedHash)); }
            catch (FormatException) { return false; }
        }

        private static VnPayIpnResultDto Ip(string code, string message) => new() { RspCode = code, Message = message };

        public async Task<BaseResponse<RefundRequestDto>> CreateRefundRequestAsync(CreateRefundRequestCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<RefundRequestDto>("Không xác định được tài khoản khách hàng.");
            if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length is < 10 or > 1000)
                return BadRequest<RefundRequestDto>("Lý do hoàn tiền cần có ít nhất 10 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RefundRequestDto>(command.OperationId, CurrentUser.Id.Value, "Refund", "Create", command.OrderId.ToString(), command) is { } replay) return replay;
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<RefundRequestDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status == WashOrderStatus.Cancelled)
                return BadRequest<RefundRequestDto>("Hoàn tiền đơn hủy được tạo tự động sau khi trừ phí hủy; không tạo yêu cầu hoàn toàn bộ giá đơn.");
            if (order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<RefundRequestDto>("Chỉ tạo yêu cầu hoàn tiền cho đơn đã thanh toán.");

            var paymentTransactions = await _transactionRepo.GetListAsync(x => x.OrderId == order.Id);
            var existing = await _refundRequestRepo.GetListAsync(x => x.OrderId == order.Id &&
                (x.Status == RefundRequestStatus.Pending || x.Status == RefundRequestStatus.Approved));
            if (existing.Count > 0)
                return BadRequest<RefundRequestDto>("Đơn hàng đã có yêu cầu hoàn tiền đang được xử lý.");

            var successfulPayment = paymentTransactions.FirstOrDefault(x =>
                x.Purpose == PaymentPurpose.Order && x.Status == PaymentTransactionStatus.Succeeded);
            if (order.PaymentMethod != PaymentMethod.Cash && successfulPayment == null)
                return BadRequest<RefundRequestDto>("Không tìm thấy giao dịch thanh toán thành công để đối chiếu.");
            var alreadyRefunded = await AsyncExecuter.SumAsync((await _refundRequestRepo.GetQueryableAsync())
                .Where(x => x.OrderId == order.Id && !x.IsDeleted && x.Status == RefundRequestStatus.Refunded).Select(x => x.Amount));
            var remaining = (successfulPayment?.Amount ?? order.FinalAmount) - alreadyRefunded;
            var amount = command.Amount ?? remaining;
            if (amount <= 0 || amount > remaining || decimal.Round(amount, 2) != amount)
                return BadRequest<RefundRequestDto>("Số tiền hoàn phải dương, tối đa hai chữ số lẻ và không vượt phần tiền gốc còn lại.");

            var request = new RefundRequest
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Amount = amount,
                OriginalPaymentTransactionId = successfulPayment?.Id,
                Reason = command.Reason.Trim(),
                Status = RefundRequestStatus.Pending
            };
            await _refundRequestRepo.InsertAsync(request, autoSave: true);
            await _notifications.RefundRequestedAsync(order, request.Amount);
            var response = Success(ToDto(request), "Đã gửi yêu cầu hoàn tiền, chờ Admin duyệt.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Refund", "Create", order.Id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        // UC 3.8.3: newest first, optional status filter, enriched with order code and customer name.
        public async Task<BaseResponse<List<RefundRequestDto>>> GetRefundRequestsAsync(bool mineOnly, RefundRequestStatus? status = null)
        {
            var requests = await _refundRequestRepo.GetQueryableAsync();
            if (mineOnly)
                requests = requests.Where(x => x.CustomerId == CurrentUser.Id);
            if (status.HasValue)
                requests = requests.Where(x => x.Status == status.Value);
            var orders = await _orders.GetQueryableAsync();
            var rows = await AsyncExecuter.ToListAsync(
                from r in requests
                join o in orders on r.OrderId equals o.Id into joined
                from o in joined.DefaultIfEmpty()
                orderby r.CreatedOn descending
                select new { r, OrderCode = o != null ? o.OrderCode : null, CustomerName = o != null ? o.CustomerName : null });
            var dtos = rows.Select(x =>
            {
                var dto = ToDto(x.r);
                dto.OrderCode = x.OrderCode;
                dto.CustomerName = x.CustomerName;
                return dto;
            }).ToList();
            await FillRefundDetailsAsync(dtos);
            return Success(dtos, dtos.Count == 0 ? "Chưa có yêu cầu hoàn tiền nào." : null);
        }

        public async Task<BaseResponse<RefundRequestDto>> GetRefundRequestAsync(Guid id)
        {
            var request = await _refundRequestRepo.FindAsync(id);
            if (request == null || (!CurrentUser.IsInRole("Admin") && request.CustomerId != CurrentUser.Id))
                return NotFound<RefundRequestDto>("Không tìm thấy yêu cầu hoàn tiền.");
            var order = await _orders.FindAsync(request.OrderId);
            var dto = ToDto(request);
            dto.OrderCode = order?.OrderCode;
            dto.CustomerName = order?.CustomerName;
            await FillRefundDetailsAsync([dto]);
            return Success(dto);
        }

        public async Task<PaginatedResult<RefundRequestDto>> FilterRefundRequestsAsync(RefundFilterQuery query)
        {
            var error = query.DateRangeError();
            var sorts = query.SortBy ?? [];
            if (error != null || query.MinAmount < 0 || query.MaxAmount < 0 || query.MinAmount > query.MaxAmount ||
                query.DateBasis is not ("created" or "reviewed" or "refunded") || sorts.Any(x => x.Field?.ToLowerInvariant() is not ("createdon" or "amount" or "reviewedat" or "refundedat" or "id")) ||
                query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error ?? "Bộ lọc không hợp lệ." };
            var q = (await _refundRequestRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status);
            if (query.OrderId.HasValue) q = q.Where(x => x.OrderId == query.OrderId);
            if (query.CustomerId.HasValue) q = q.Where(x => x.CustomerId == query.CustomerId);
            if (query.FromDate > 0)
            {
                var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime;
                q = query.DateBasis switch { "reviewed" => q.Where(x => x.ReviewedAt >= from), "refunded" => q.Where(x => x.RefundedAt >= from), _ => q.Where(x => x.CreatedOn >= query.FromDate) };
            }
            if (query.ToDate > 0)
            {
                var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime;
                q = query.DateBasis switch { "reviewed" => q.Where(x => x.ReviewedAt <= to), "refunded" => q.Where(x => x.RefundedAt <= to), _ => q.Where(x => x.CreatedOn <= query.ToDate) };
            }
            if (query.MerchantId.HasValue)
            {
                var orders = await _orders.GetQueryableAsync();
                q = q.Where(r => r.FinancialSnapshotBasis != null ? r.MerchantIdSnapshot == query.MerchantId : orders.Any(o => o.Id == r.OrderId && o.MerchantId == query.MerchantId));
            }
            if (query.MinAmount.HasValue) q = q.Where(x => x.Amount >= query.MinAmount);
            if (query.MaxAmount.HasValue) q = q.Where(x => x.Amount <= query.MaxAmount);
            var joined = from r in q join o in await _orders.GetQueryableAsync() on r.OrderId equals o.Id
                         select new { r, o.OrderCode, o.CustomerName };
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim().ToLower();
                joined = joined.Where(x => x.OrderCode.ToLower().Contains(keyword) || x.CustomerName.ToLower().Contains(keyword) || x.r.RefundReference != null && x.r.RefundReference.ToLower().Contains(keyword));
            }
            var total = await AsyncExecuter.CountAsync(joined);
            var page = Math.Max(1, query.Page); var take = Math.Clamp(query.Take, 1, 100);
            var ordered = joined.OrderByDescending(x => x.r.CreatedOn);
            for (var i = 0; i < sorts.Length; i++)
            {
                var descending = sorts[i].IsDescending; var first = i == 0;
                ordered = sorts[i].Field.ToLowerInvariant() switch
                {
                    "amount" => first ? descending ? joined.OrderByDescending(x => x.r.Amount) : joined.OrderBy(x => x.r.Amount) : descending ? ordered.ThenByDescending(x => x.r.Amount) : ordered.ThenBy(x => x.r.Amount),
                    "reviewedat" => first ? descending ? joined.OrderByDescending(x => x.r.ReviewedAt) : joined.OrderBy(x => x.r.ReviewedAt) : descending ? ordered.ThenByDescending(x => x.r.ReviewedAt) : ordered.ThenBy(x => x.r.ReviewedAt),
                    "refundedat" => first ? descending ? joined.OrderByDescending(x => x.r.RefundedAt) : joined.OrderBy(x => x.r.RefundedAt) : descending ? ordered.ThenByDescending(x => x.r.RefundedAt) : ordered.ThenBy(x => x.r.RefundedAt),
                    "id" => first ? descending ? joined.OrderByDescending(x => x.r.Id) : joined.OrderBy(x => x.r.Id) : descending ? ordered.ThenByDescending(x => x.r.Id) : ordered.ThenBy(x => x.r.Id),
                    _ => first ? descending ? joined.OrderByDescending(x => x.r.CreatedOn) : joined.OrderBy(x => x.r.CreatedOn) : descending ? ordered.ThenByDescending(x => x.r.CreatedOn) : ordered.ThenBy(x => x.r.CreatedOn)
                };
            }
            var rows = await AsyncExecuter.ToListAsync(ordered.ThenBy(x => x.r.Id).Skip((page - 1) * take).Take(take));
            var dtos = rows.Select(x => { var dto = ToDto(x.r); dto.OrderCode = x.OrderCode; dto.CustomerName = x.CustomerName; return dto; }).ToList();
            await FillRefundDetailsAsync(dtos);
            return Success(dtos, total, page, take);
        }

        private async Task FillRefundDetailsAsync(List<RefundRequestDto> rows)
        {
            if (rows.Count == 0) return;
            var ids = rows.Select(x => x.OrderId).Distinct().ToList();
            var orders = (await _orders.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
            var transactions = await _transactionRepo.GetListAsync(x => ids.Contains(x.OrderId) && x.Purpose == PaymentPurpose.Order &&
                (x.Status == PaymentTransactionStatus.Succeeded || x.Status == PaymentTransactionStatus.Refunded));
            var refunds = await AsyncExecuter.ToListAsync((await _refundRequestRepo.GetQueryableAsync()).Where(x => ids.Contains(x.OrderId) && !x.IsDeleted && x.Status == RefundRequestStatus.Refunded)
                .GroupBy(x => x.OrderId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }));
            var users = new Dictionary<Guid, User>();
            if (CurrentUser.IsInRole("Admin"))
            {
                var userIds = rows.Select(x => x.CustomerId).Distinct().ToList();
                users = (await LazyServiceProvider.LazyGetRequiredService<IRepository<User, Guid>>().GetListAsync(x => userIds.Contains(x.Id))).ToDictionary(x => x.Id);
            }
            foreach (var dto in rows)
            {
                if (!orders.TryGetValue(dto.OrderId, out var order)) continue;
                var transaction = dto.OriginalPaymentTransactionId.HasValue ? transactions.FirstOrDefault(x => x.Id == dto.OriginalPaymentTransactionId) : transactions.Where(x => x.OrderId == dto.OrderId).OrderBy(x => x.CompletedAt).ThenBy(x => x.Id).FirstOrDefault();
                dto.OrderCode = order.OrderCode; dto.CustomerName = order.CustomerName;
                dto.MerchantId = transaction?.FinancialSnapshotBasis != null ? transaction.MerchantIdSnapshot : order.MerchantId;
                dto.PaymentMethod = transaction?.PaymentMethod ?? order.PaymentMethod;
                dto.OriginalAmount = transaction?.Amount ?? (order.PaymentMethod == PaymentMethod.Cash ? order.FinalAmount : null);
                dto.RemainingRefundableAmount = dto.OriginalAmount.HasValue ? Math.Max(0, dto.OriginalAmount.Value - (refunds.FirstOrDefault(x => x.Id == order.Id)?.Amount ?? 0) -
                    (order.Status == WashOrderStatus.Cancelled ? order.CancelFeePaidAmount : 0)) : null;
                if (users.TryGetValue(dto.CustomerId, out var customer)) { dto.CustomerEmail = customer.Email; dto.CustomerPhoneNumber = customer.Phone; dto.CustomerAvatarUrl = customer.Avatar; }
            }
            var merchantIds = rows.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value).Distinct().ToList();
            var merchants = (await LazyServiceProvider.LazyGetRequiredService<IRepository<Merchant, Guid>>().GetListAsync(x => merchantIds.Contains(x.Id))).ToDictionary(x => x.Id);
            foreach (var dto in rows) if (dto.MerchantId is { } merchantId) dto.MerchantName = merchants.GetValueOrDefault(merchantId)?.BusinessName;
        }

        public async Task<BaseResponse<RefundRequestDto>> ReviewRefundRequestAsync(Guid id, ReviewRefundRequestCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RefundRequestDto>(command.OperationId, CurrentUser.Id!.Value, "Finance", "ReviewRefund", id.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync($"refund-request:{id}");
            var request = await _refundRequestRepo.FindAsync(id);
            if (request == null)
                return NotFound<RefundRequestDto>("Không tìm thấy yêu cầu hoàn tiền.");
            if (request.Status != RefundRequestStatus.Pending)
                return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.Conflict, "AlreadyReviewed", "Yêu cầu này đã được xử lý trước đó.");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != ToDto(request).Version) return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.Conflict, "VersionMismatch", "Yêu cầu hoàn tiền đã thay đổi.");
            if (command.ReviewNote?.Trim().Length > 1000 || (!command.Approve && string.IsNullOrWhiteSpace(command.ReviewNote))) return BadRequest<RefundRequestDto>("Lý do từ chối bắt buộc; ghi chú tối đa 1000 ký tự.");

            request.Status = command.Approve ? RefundRequestStatus.Approved : RefundRequestStatus.Rejected;
            request.ReviewNote = string.IsNullOrWhiteSpace(command.ReviewNote) ? null : command.ReviewNote.Trim();
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = DateTime.UtcNow;
            await _refundRequestRepo.UpdateAsync(request, autoSave: true);
            var message = command.Approve
                ? "Yêu cầu được duyệt. Admin cần hoàn tiền ngoài hệ thống rồi xác nhận mã tham chiếu."
                : "Yêu cầu hoàn tiền đã bị từ chối.";
            await Audit.LogAsync(new AuditEntry("Refund", command.Approve ? "ApproveRefund" : "RejectRefund", "RefundRequest", request.Id.ToString(), null)
            {
                Reason = request.ReviewNote, Details = new { orderId = request.OrderId, request.Amount }
            });
            await _notifications.SendAsync([request.CustomerId], NotificationType.RefundReviewed, "Kết quả yêu cầu hoàn tiền", message, "refund-request", request.Id);
            var response = Success(ToDto(request), message);
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "ReviewRefund", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<RefundRequestDto>> ConfirmRefundedAsync(Guid id, ConfirmRefundCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<RefundRequestDto>(command.OperationId, CurrentUser.Id!.Value, "Finance", "ConfirmRefund", id.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync($"refund-request:{id}");
            var request = await _refundRequestRepo.FindAsync(id);
            if (request == null)
                return NotFound<RefundRequestDto>("Không tìm thấy yêu cầu hoàn tiền.");
            if (request.Status == RefundRequestStatus.Refunded)
            {
                if (request.RefundReference != command.RefundReference?.Trim()) return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.Conflict, "RefundReferenceChanged", "Đã hoàn tiền với mã tham chiếu khác.");
                var old = Success(ToDto(request), "Yêu cầu này đã được hoàn tiền.");
                await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "ConfirmRefund", id.ToString(), command, old);
                await uow.CompleteAsync(); return old;
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != ToDto(request).Version) return AdministrativeWriteGuard.Error<RefundRequestDto>(HttpStatusCode.Conflict, "VersionMismatch", "Yêu cầu hoàn tiền đã thay đổi.");
            if (request.Status != RefundRequestStatus.Approved)
                return BadRequest<RefundRequestDto>("Chỉ xác nhận hoàn tiền sau khi yêu cầu đã được duyệt.");
            if (string.IsNullOrWhiteSpace(command.RefundReference) || command.RefundReference.Trim().Length > 120)
                return BadRequest<RefundRequestDto>("Cần nhập mã tham chiếu giao dịch hoàn tiền.");

            // Match the payment callbacks' lock order: transaction, then order.
            var reference = await AsyncExecuter.FirstOrDefaultAsync((await _transactionRepo.GetQueryableAsync())
                .Where(x => x.OrderId == request.OrderId && x.Purpose == PaymentPurpose.Order &&
                    x.Status == PaymentTransactionStatus.Succeeded).Select(x => x.TransactionReference));
            var successful = reference == null ? null : await _transactionRepo.GetForUpdateByReferenceAsync(reference);
            var order = await _orders.GetForUpdateAsync(request.OrderId);
            if (order == null || order.PaymentStatus != PaymentStatus.Paid ||
                successful != null && successful.Status != PaymentTransactionStatus.Succeeded)
                return BadRequest<RefundRequestDto>("Đơn không còn ở trạng thái đã thanh toán.");
            if (order.PaymentMethod != PaymentMethod.Cash && successful == null)
                return BadRequest<RefundRequestDto>("Không tìm thấy giao dịch thanh toán gốc thành công.");

            var refundable = (successful?.Amount ?? order.FinalAmount) -
                (order.Status == WashOrderStatus.Cancelled ? order.CancelFeePaidAmount : 0m);
            var alreadyRefunded = await AsyncExecuter.SumAsync((await _refundRequestRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && x.OrderId == order.Id && x.Status == RefundRequestStatus.Refunded).Select(x => x.Amount));
            refundable -= alreadyRefunded;
            if (request.Amount <= 0m || request.Amount > refundable || decimal.Round(request.Amount, 2) != request.Amount ||
                (request.OriginalPaymentTransactionId.HasValue && request.OriginalPaymentTransactionId != successful?.Id))
                return BadRequest<RefundRequestDto>("Số tiền hoàn vượt quá phần còn lại sau khi trừ phí hủy.");
            if (successful != null)
            {
                successful.Status = request.Amount == refundable ? PaymentTransactionStatus.Refunded : PaymentTransactionStatus.Succeeded;
                successful.FailureReason = null;
                await _transactionRepo.UpdateAsync(successful, autoSave: true);
            }
            order.PaymentStatus = request.Amount == refundable ? PaymentStatus.Refunded : PaymentStatus.Paid;
            await _orders.UpdateAsync(order, autoSave: true);

            request.Status = RefundRequestStatus.Refunded;
            request.OriginalPaymentTransactionId = successful?.Id;
            request.MerchantIdSnapshot = successful?.FinancialSnapshotBasis != null ? successful.MerchantIdSnapshot : order.MerchantId;
            request.CommissionRateSnapshot = successful?.FinancialSnapshotBasis != null ? successful.CommissionRateSnapshot : order.CommissionRateApplied;
            request.FinancialSnapshotBasis = successful?.FinancialSnapshotBasis ?? "confirmation-baseline";
            request.RefundReference = command.RefundReference.Trim();
            request.RefundedAt = DateTime.UtcNow;
            await _refundRequestRepo.UpdateAsync(request, autoSave: true);
            await Audit.LogAsync(new AuditEntry("Refund", "ConfirmRefunded", "RefundRequest", request.Id.ToString(), order.OrderCode)
            {
                Details = new { orderId = order.Id, request.Amount, refundReference = request.RefundReference }
            });
            await _notifications.SendAsync([request.CustomerId], NotificationType.RefundConfirmed, "Đã hoàn tiền", $"Yêu cầu #{request.Id}: đã hoàn {request.Amount:N0}đ, mã {request.RefundReference}.", "refund-request", request.Id);
            var response = Success(ToDto(request), "Đã ghi nhận Admin xác nhận hoàn tiền.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "ConfirmRefund", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        private static RefundRequestDto ToDto(RefundRequest request) => new()
        {
            Version = AdministrativeWriteGuard.Version(new { request.Id, request.Status, request.Amount, request.ReviewNote, ReviewedAt = request.ReviewedAt?.Ticks / 10, request.RefundReference, RefundedAt = request.RefundedAt?.Ticks / 10, request.OriginalPaymentTransactionId }),
            OriginalPaymentTransactionId = request.OriginalPaymentTransactionId,
            SettlementId = request.SettlementId,
            Id = request.Id,
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            Reason = request.Reason,
            Status = request.Status,
            ReviewNote = request.ReviewNote,
            ReviewedBy = request.ReviewedBy,
            ReviewedAt = request.ReviewedAt,
            RefundReference = request.RefundReference,
            RefundedAt = request.RefundedAt,
            CreatedOn = request.CreatedOn
        };
    }
}
