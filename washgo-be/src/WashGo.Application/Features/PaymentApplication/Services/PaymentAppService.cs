using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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

namespace WashGo.Application.Features.PaymentApplication.Services
{
    public class PaymentAppService : WashGoAppService, IPaymentAppService
    {
        private readonly IWashOrderAppService _orderService;
        private readonly IPaymentTransactionRepository _transactionRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRequestRepo;
        private readonly IConfiguration _configuration;
        private readonly IWashOrderRepository _orders;

        public PaymentAppService(
            IWashOrderAppService orderService,
            IPaymentTransactionRepository transactionRepo,
            IConfiguration configuration,
            IWashOrderRepository orders,
            IRepository<RefundRequest, Guid> refundRequestRepo)
        {
            _orderService = orderService;
            _transactionRepo = transactionRepo;
            _configuration = configuration;
            _orders = orders;
            _refundRequestRepo = refundRequestRepo;
        }

        public async Task<BaseResponse<PaymentResultDto>> CreatePaymentAsync(CreatePaymentCommand command)
        {
            var orderResponse = await _orderService.GetByIdAsync(command.OrderId);
            if (!orderResponse.Succeeded || orderResponse.Data == null)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng cần thanh toán.");

            var order = orderResponse.Data;
            if (order.PaymentStatus == PaymentStatus.Paid)
                return BadRequest<PaymentResultDto>("Đơn hàng đã được thanh toán.");

            if (order.Status != WashOrderStatus.AwaitingPayment)
                return BadRequest<PaymentResultDto>("Đơn hàng chưa ở trạng thái chờ thanh toán.");
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
                return BadRequest<PaymentResultDto>("Đơn chưa được Merchant cân thực tế và chốt giá.");

            if (command.PaymentMethod == PaymentMethod.Cash)
                return BadRequest<PaymentResultDto>("Payment URL chỉ áp dụng cho phương thức thanh toán trực tuyến.");

            if (command.PaymentMethod != PaymentMethod.VnPay)
                return BadRequest<PaymentResultDto>("Hiện chỉ hỗ trợ tạo URL cho phương thức VNPay.");

            var transactionReference = $"WG-PAY-{order.OrderCode}-{Guid.NewGuid():N}";
            var useVnPay = string.Equals(_configuration["Payment:Provider"], "VnPay", StringComparison.OrdinalIgnoreCase);
            var paymentUrl = useVnPay
                ? BuildVnPayUrl(order.OrderCode, transactionReference, order.FinalAmount, command.ClientIpAddress)
                : $"http://localhost:5000/payment/mock-checkout?orderId={order.Id}&reference={transactionReference}";
            if (string.IsNullOrWhiteSpace(paymentUrl))
                return BadRequest<PaymentResultDto>("Chưa cấu hình đầy đủ thông tin kết nối VNPay.");

            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
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
            }, useVnPay ? "Tạo URL thanh toán VNPay thành công." : "Tạo payment URL sandbox thành công.");
        }

        public async Task<BaseResponse<PaymentResultDto>> CreateOverdueFeePaymentAsync(CreateOverdueFeePaymentCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<PaymentResultDto>("Không xác định được tài khoản khách hàng.");
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.PaymentStatus != PaymentStatus.Paid ||
                order.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage))
                return BadRequest<PaymentResultDto>("Phí lưu kho chỉ thanh toán khi đồ đã được bỏ vào locker chờ nhận.");
            if (command.PaymentMethod != PaymentMethod.VnPay)
                return BadRequest<PaymentResultDto>("Hiện chỉ hỗ trợ thanh toán phí lưu kho qua VNPay.");

            var overdueTotal = OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow);
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

            var useVnPay = string.Equals(_configuration["Payment:Provider"], "VnPay", StringComparison.OrdinalIgnoreCase);
            var reference = $"WG-FEE-{order.OrderCode}-{Guid.NewGuid():N}";
            var paymentUrl = useVnPay
                ? BuildVnPayUrl(order.OrderCode, reference, outstanding, command.ClientIpAddress,
                    $"Overdue storage fee {order.OrderCode}")
                : $"http://localhost:5000/payment/mock-checkout?orderId={order.Id}&reference={reference}";
            if (string.IsNullOrWhiteSpace(paymentUrl))
                return BadRequest<PaymentResultDto>("Chưa cấu hình đầy đủ thông tin kết nối VNPay.");

            await _orders.UpdateAsync(order, autoSave: true);
            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
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
            if (command.PaymentMethod != PaymentMethod.VnPay)
                return BadRequest<PaymentResultDto>("Hiện chỉ hỗ trợ thanh toán phí hủy qua VNPay.");

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

            var useVnPay = string.Equals(_configuration["Payment:Provider"], "VnPay", StringComparison.OrdinalIgnoreCase);
            var reference = $"WG-CANCEL-{order.OrderCode}-{Guid.NewGuid():N}";
            var paymentUrl = useVnPay
                ? BuildVnPayUrl(order.OrderCode, reference, outstanding, command.ClientIpAddress,
                    $"Cancellation fee {order.OrderCode}")
                : $"http://localhost:5000/payment/mock-checkout?orderId={order.Id}&reference={reference}";
            if (string.IsNullOrWhiteSpace(paymentUrl))
                return BadRequest<PaymentResultDto>("Chưa cấu hình đầy đủ thông tin kết nối VNPay.");

            await _orders.UpdateAsync(order, autoSave: true);
            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
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
            if (transaction.PaymentMethod != PaymentMethod.VnPay ||
                string.Equals(_configuration["Payment:Provider"], "VnPay", StringComparison.OrdinalIgnoreCase))
                return BadRequest<PaymentResultDto>("Callback sandbox không được phép xác nhận giao dịch VNPay.");

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
                    orderEntity.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage))
                    return BadRequest<PaymentResultDto>("Đơn không còn chờ khách nhận tại locker.");

                var due = OverdueFeeCalculator.GetTotalStorageDue(orderEntity, DateTime.UtcNow);
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
            if (responseCode != "00" || transactionStatus != "00")
            {
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureReason = $"VNPay response={responseCode}, status={transactionStatus}";
                transaction.ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo");
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                await uow.CompleteAsync();
                return Ip("00", "Confirm Success");
            }

            var order = await _orders.GetForUpdateAsync(transaction.OrderId);
            if (order == null)
                return Ip("01", "Order not found");

            if (transaction.Purpose == PaymentPurpose.CancellationFee)
            {
                if (!await MarkCancellationFeePaidAsync(order, transaction, "VNPay"))
                    return Ip("04", "Invalid cancellation fee");
                transaction.Status = PaymentTransactionStatus.Succeeded;
                transaction.ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo");
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                await uow.CompleteAsync();
                return Ip("00", "Confirm Success");
            }
            if (transaction.Purpose == PaymentPurpose.OverdueStorageFee)
            {
                var orderEntity = await _orders.GetForUpdateAsync(transaction.OrderId);
                if (orderEntity == null)
                    return Ip("01", "Order not found");
                if (orderEntity.PaymentStatus != PaymentStatus.Paid ||
                    orderEntity.Status is not (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage))
                    return Ip("04", "Invalid order state");
                var due = OverdueFeeCalculator.GetTotalStorageDue(orderEntity, DateTime.UtcNow);
                if (transaction.Amount > due - orderEntity.StorageFeeTotal)
                    return Ip("04", "Invalid amount");
                orderEntity.OverdueFeeTotal = due;
                orderEntity.StorageFeeTotal += transaction.Amount;
                await _orders.UpdateAsync(orderEntity, autoSave: true);
                transaction.Status = PaymentTransactionStatus.Succeeded;
                transaction.ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo");
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, autoSave: true);
                await uow.CompleteAsync();
                return Ip("00", "Confirm Success");
            }
            if (transaction.Purpose != PaymentPurpose.Order)
                return Ip("04", "Invalid payment purpose");
            if (order.PaymentStatus == PaymentStatus.Paid)
                return Ip("02", "Order already confirmed");
            if (!await MarkOrderPaidAsync(order, transaction, "VNPay",
                    parameters.GetValueOrDefault("vnp_TransactionNo")))
                return Ip("04", "Invalid order state or amount");

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo");
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);
            await uow.CompleteAsync();
            return Ip("00", "Confirm Success");
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
                order.Status != WashOrderStatus.AwaitingPayment || order.PaymentStatus != PaymentStatus.Unpaid ||
                order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
                return false;

            order.Status = WashOrderStatus.Paid;
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.AwaitingPayment,
                Status = WashOrderStatus.Paid,
                ActorName = provider,
                ActorRole = "PaymentGateway",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Thanh toán thành công, mã giao dịch: {providerReference ?? transaction.TransactionReference}"
            });
            await _orders.UpdateAsync(order, autoSave: true);
            return true;
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
            if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length < 10)
                return BadRequest<RefundRequestDto>("Lý do hoàn tiền cần có ít nhất 10 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
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

            var request = new RefundRequest
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Amount = successfulPayment?.Amount ?? order.FinalAmount,
                Reason = command.Reason.Trim(),
                Status = RefundRequestStatus.Pending
            };
            await _refundRequestRepo.InsertAsync(request, autoSave: true);
            await uow.CompleteAsync();
            return Success(ToDto(request), "Đã gửi yêu cầu hoàn tiền, chờ Admin duyệt.");
        }

        public async Task<BaseResponse<List<RefundRequestDto>>> GetRefundRequestsAsync(bool mineOnly)
        {
            var items = mineOnly
                ? await _refundRequestRepo.GetListAsync(x => x.CustomerId == CurrentUser.Id)
                : await _refundRequestRepo.GetListAsync();
            return Success(items.OrderByDescending(x => x.CreatedOn).Select(ToDto).ToList());
        }

        public async Task<BaseResponse<RefundRequestDto>> ReviewRefundRequestAsync(Guid id, ReviewRefundRequestCommand command)
        {
            var request = await _refundRequestRepo.FindAsync(id);
            if (request == null)
                return NotFound<RefundRequestDto>("Không tìm thấy yêu cầu hoàn tiền.");
            if (request.Status != RefundRequestStatus.Pending)
                return BadRequest<RefundRequestDto>("Yêu cầu này đã được xử lý trước đó.");

            request.Status = command.Approve ? RefundRequestStatus.Approved : RefundRequestStatus.Rejected;
            request.ReviewNote = string.IsNullOrWhiteSpace(command.ReviewNote) ? null : command.ReviewNote.Trim();
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = DateTime.UtcNow;
            await _refundRequestRepo.UpdateAsync(request, autoSave: true);
            var message = command.Approve
                ? "Yêu cầu được duyệt. Admin cần hoàn tiền ngoài hệ thống rồi xác nhận mã tham chiếu."
                : "Yêu cầu hoàn tiền đã bị từ chối.";
            return Success(ToDto(request), message);
        }

        public async Task<BaseResponse<RefundRequestDto>> ConfirmRefundedAsync(Guid id, ConfirmRefundCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var request = await _refundRequestRepo.FindAsync(id);
            if (request == null)
                return NotFound<RefundRequestDto>("Không tìm thấy yêu cầu hoàn tiền.");
            if (request.Status != RefundRequestStatus.Approved)
                return BadRequest<RefundRequestDto>("Chỉ xác nhận hoàn tiền sau khi yêu cầu đã được duyệt.");
            if (string.IsNullOrWhiteSpace(command.RefundReference))
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
            if (request.Amount <= 0m || request.Amount > refundable)
                return BadRequest<RefundRequestDto>("Số tiền hoàn vượt quá phần còn lại sau khi trừ phí hủy.");
            if (successful != null)
            {
                successful.Status = PaymentTransactionStatus.Refunded;
                successful.FailureReason = null;
                await _transactionRepo.UpdateAsync(successful, autoSave: true);
            }
            order.PaymentStatus = PaymentStatus.Refunded;
            await _orders.UpdateAsync(order, autoSave: true);

            request.Status = RefundRequestStatus.Refunded;
            request.RefundReference = command.RefundReference.Trim();
            request.RefundedAt = DateTime.UtcNow;
            await _refundRequestRepo.UpdateAsync(request, autoSave: true);
            await uow.CompleteAsync();
            return Success(ToDto(request), "Đã ghi nhận Admin xác nhận hoàn tiền.");
        }

        private static RefundRequestDto ToDto(RefundRequest request) => new()
        {
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
