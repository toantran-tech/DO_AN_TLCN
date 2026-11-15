using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService
{
    private async Task<BaseResponse<PaymentResultDto>> CreateSessionAsync(CreatePaymentCommand command, PaymentPurpose purpose)
    {
        if (!CurrentUser.Id.HasValue)
            return BadRequest<PaymentResultDto>("Không xác định được tài khoản khách hàng.");
        if (command.PaymentMethod is not (PaymentMethod.VnPay or PaymentMethod.Momo))
            return BadRequest<PaymentResultDto>("Chỉ hỗ trợ thanh toán trực tuyến qua VNPay hoặc MoMo.");

        string reference;
        string orderCode;
        decimal amount;
        using (var prepare = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            var due = await PaymentDueAsync(order, purpose);
            if (due.Error != null) return BadRequest<PaymentResultDto>(due.Error);
            amount = due.Amount;
            orderCode = order.OrderCode;
            // Do not take transaction locks after the order lock. All creators hold
            // this order lock; callbacks/finalizers consistently lock transaction -> order.
            var pending = await _transactionRepo.GetListAsync(x => x.OrderId == order.Id &&
                x.Purpose == purpose && x.Status == PaymentTransactionStatus.Pending && !x.IsDeleted);
            if (pending.Count > 1)
                return SessionConflict("LegacyPaymentsRequireReview", "Đơn có nhiều giao dịch cũ đang chờ; cần đối soát trước khi tạo URL mới.");
            if (pending.Count == 1)
            {
                var existing = pending[0];
                if (existing.PaymentMethod != command.PaymentMethod)
                    return SessionConflict("PaymentMethodConflict", "Đã có phiên thanh toán bằng phương thức khác. Vui lòng dùng phiên hiện tại hoặc chờ xác minh.");
                if (existing.Amount != amount)
                    return SessionConflict("PaymentAmountChanged", "Số tiền phải trả đã thay đổi; cần xác minh phiên trước khi tạo giao dịch mới.");
                if (existing.SessionState == PaymentSessionState.Unknown)
                    return SessionConflict("PaymentVerificationRequired", "Kết quả tạo phiên chưa xác định. Không tạo thêm giao dịch; vui lòng chờ xác minh.");
                if (string.IsNullOrWhiteSpace(existing.PaymentUrl))
                    return SessionConflict(existing.CreatedOn < DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds()
                        ? "PaymentVerificationRequired" : "PaymentInitializing", "Phiên đang khởi tạo hoặc cần xác minh. Vui lòng tải lại sau.");
                // VNPay URLs explicitly include this expiry. Expired locally does not
                // prove no money was received; retain the reference for reconciliation.
                if (existing.PaymentMethod == PaymentMethod.VnPay && UseLiveGateway &&
                    existing.CreatedOn < DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds())
                    return SessionConflict("PaymentVerificationRequired", "URL VNPay đã quá thời hạn; cần xác minh giao dịch trước khi tạo phiên thay thế.");
                return new BaseResponse<PaymentResultDto>
                {
                    Succeeded = true, StatusCode = HttpStatusCode.OK,
                    Data = PaymentView(order, existing), Message = "Sử dụng lại phiên thanh toán hiện tại.",
                    Meta = new { code = "PaymentSessionReused", reused = true }
                };
            }

            reference = NewReference(purpose switch
            {
                PaymentPurpose.OverdueStorageFee => "FEE", PaymentPurpose.CancellationFee => "CANCEL", _ => "PAY"
            }, order.OrderCode, command.PaymentMethod);
            await _transactionRepo.InsertAsync(new PaymentTransaction
            {
                OrderId = order.Id, TransactionReference = reference, PaymentMethod = command.PaymentMethod,
                Purpose = purpose, Amount = amount, Status = PaymentTransactionStatus.Pending,
                SessionState = PaymentSessionState.Creating, IsActiveSession = true,
                MerchantIdSnapshot = order.MerchantId, CommissionRateSnapshot = order.CommissionRateApplied,
                FinancialSnapshotBasis = "payment-created"
            }, autoSave: true);
            await prepare.CompleteAsync();
        }

        // The reference is durable before HTTP. Never retry a gateway POST under a new
        // reference after a timeout; a crash leaves Creating and blocks another session.
        var result = await GatewayUrlAsync(command.OrderId, command.PaymentMethod, reference,
            orderCode, amount, command.ClientIpAddress);
        using var finalize = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var transaction = await _transactionRepo.GetForUpdateByReferenceAsync(reference);
        var current = await _orders.GetForUpdateAsync(command.OrderId);
        if (transaction == null || current == null)
            return SessionConflict("PaymentVerificationRequired", "Không tìm thấy phiên vừa tạo; cần xác minh giao dịch.");
        if (transaction.Status != PaymentTransactionStatus.Pending)
        {
            await finalize.CompleteAsync();
            return Success(PaymentView(current, transaction), "Kết quả thanh toán đã được nhận trước khi tạo URL hoàn tất.");
        }
        if (result.Url == null)
        {
            transaction.SessionState = result.IsUncertain ? PaymentSessionState.Unknown : PaymentSessionState.Closed;
            transaction.Status = result.IsUncertain ? PaymentTransactionStatus.Pending : PaymentTransactionStatus.Failed;
            transaction.IsActiveSession = result.IsUncertain;
            transaction.FailureReason = result.Error == null ? null : result.Error[..Math.Min(result.Error.Length, 500)];
            if (!result.IsUncertain) transaction.CompletedAt = DateTime.UtcNow;
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);
            await finalize.CompleteAsync();
            return result.IsUncertain
                ? SessionConflict("PaymentVerificationRequired", "Chưa xác định kết quả từ cổng. Không tạo lại giao dịch; vui lòng chờ xác minh.")
                : BadRequest<PaymentResultDto>(result.Error ?? "Cổng từ chối tạo phiên thanh toán.");
        }
        transaction.PaymentUrl = result.Url;
        transaction.SessionState = PaymentSessionState.Ready;
        await _transactionRepo.UpdateAsync(transaction, autoSave: true);
        var latestDue = await PaymentDueAsync(current, purpose);
        await finalize.CompleteAsync();
        if (latestDue.Error != null || latestDue.Amount != amount)
            return SessionConflict("PaymentStateChanged", "Đơn đã thay đổi trong lúc tạo phiên; giao dịch được giữ lại để xác minh, không mở URL mới.");
        return Success(PaymentView(current, transaction), "Đã tạo phiên thanh toán.");
    }

    private async Task<(decimal Amount, string? Error)> PaymentDueAsync(WashOrder order, PaymentPurpose purpose)
    {
        if (purpose == PaymentPurpose.Order)
        {
            if (order.PaymentStatus != PaymentStatus.Unpaid ||
                order.Status is not (WashOrderStatus.AwaitingPayment or WashOrderStatus.MovedToStorage))
                return (0, "Đơn không còn ở trạng thái chờ thanh toán tiền giặt.");
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
                return (0, "Đơn chưa được cân và chốt giá.");
            return (order.FinalAmount, order.FinalAmount > 0 ? null : "Giá đơn không hợp lệ.");
        }
        if (purpose == PaymentPurpose.CancellationFee)
        {
            var fee = order.CancelFeeAmount - order.CancelFeePaidAmount;
            return (fee, order.Status == WashOrderStatus.Cancelled && fee > 0 ? null : "Đơn không còn phí hủy cần thanh toán.");
        }
        if (order.PaymentStatus != PaymentStatus.Paid || order.Status is not
            (WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage or WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage))
            return (0, "Đơn không ở trạng thái được thanh toán phí lưu kho.");
        var outstanding = OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow,
            await _settings.ForOrderAsync(order)) - order.StorageFeeTotal;
        return (outstanding, outstanding > 0 ? null : "Đơn không còn phí lưu kho cần thanh toán.");
    }

    private static BaseResponse<PaymentResultDto> SessionConflict(string code, string message) => new()
    {
        Succeeded = false, StatusCode = HttpStatusCode.Conflict, Message = message,
        Meta = new { code, retryAfterSeconds = code == "PaymentInitializing" ? 2 : (int?)null }
    };

    private static PaymentResultDto PaymentView(WashOrder order, PaymentTransaction transaction) => new()
    {
        OrderId = order.Id, OrderCode = order.OrderCode, Amount = transaction.Amount,
        Purpose = transaction.Purpose, PaymentMethod = transaction.PaymentMethod, PaymentStatus = order.PaymentStatus,
        TransactionReference = transaction.TransactionReference, PaymentUrl = transaction.PaymentUrl ?? string.Empty,
        TransactionStatus = transaction.Status, SessionState = transaction.SessionState,
        RequiresReconciliation = transaction.RequiresReconciliation
    };
}
