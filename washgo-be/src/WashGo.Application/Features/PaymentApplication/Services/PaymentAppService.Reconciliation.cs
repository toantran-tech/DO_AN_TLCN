using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService
{
    private async Task MarkPendingVerificationAsync(PaymentTransaction transaction, string reason, string? providerId)
    {
        if (transaction.Status != PaymentTransactionStatus.Pending || transaction.GatewayVerifiedAt.HasValue) return;
        transaction.SessionState = PaymentSessionState.Unknown;
        transaction.FailureReason = reason;
        transaction.ReconciliationReason = reason;
        transaction.ProviderTransactionId ??= providerId;
        await _transactionRepo.UpdateAsync(transaction, autoSave: true);
    }

    private async Task RecordUnappliedReceiptAsync(PaymentTransaction transaction, WashOrder? order, string reason)
    {
        transaction.Status = PaymentTransactionStatus.RequiresReview;
        transaction.RequiresReconciliation = true;
        transaction.ReconciliationReason = reason;
        await _transactionRepo.UpdateAsync(transaction, autoSave: true);
        Guid? refundId = null;
        if (order != null && !await _refundRequestRepo.AnyAsync(x =>
            x.OriginalPaymentTransactionId == transaction.Id && x.IsExcessPayment && !x.IsDeleted))
        {
            var refund = new RefundRequest
            {
                OrderId = order.Id, CustomerId = order.CustomerId, Amount = transaction.Amount,
                OriginalPaymentTransactionId = transaction.Id, IsExcessPayment = true,
                FinancialSnapshotBasis = "unapplied-receipt", MerchantIdSnapshot = null, CommissionRateSnapshot = 0,
                Reason = $"Tiền cổng đã xác nhận nhưng chưa áp dụng vào đơn ({reason}); giao dịch {transaction.TransactionReference}.",
                Status = RefundRequestStatus.Pending
            };
            await _refundRequestRepo.InsertAsync(refund, autoSave: true);
            refundId = refund.Id;
        }
        await Audit.LogAsync(new AuditEntry("Payment", "ReceiptRequiresReview", "PaymentTransaction", transaction.Id.ToString(), order?.OrderCode)
        {
            Details = new { transaction.OrderId, transaction.TransactionReference, transaction.Amount, reason }
        });
        await _notifications.SendToAdminsAsync(NotificationType.RefundRequested, "Giao dịch cần đối soát",
            $"Giao dịch {transaction.TransactionReference}: đã nhận {transaction.Amount:N0}đ nhưng chưa áp dụng vào đơn; {reason}.",
            refundId.HasValue ? "refund-request" : "payment-reconciliation", refundId ?? transaction.Id);
    }

    public async Task<PaginatedResult<PaymentReconciliationDto>> GetReconciliationsAsync(int page, int take, bool includeResolved)
    {
        if (!CurrentUser.IsInRole("Admin"))
            return new PaginatedResult<PaymentReconciliationDto>
            { Succeeded = false, StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Admin được đối soát giao dịch." };
        page = Math.Clamp(page, 1, 100000); take = Math.Clamp(take, 1, 100);
        var stale = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds();
        var vnpExpiry = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds();
        var live = UseLiveGateway;
        var source = await _transactionRepo.GetQueryableAsync();
        var refunds = await _refundRequestRepo.GetQueryableAsync();
        var query = source.Where(x => !x.IsDeleted &&
            ((x.RequiresReconciliation && (includeResolved || x.ReconciledAt == null)) ||
             (x.Status == PaymentTransactionStatus.Pending &&
              (x.SessionState == PaymentSessionState.Unknown || x.SessionState == PaymentSessionState.Legacy ||
               (live && x.PaymentMethod == PaymentMethod.VnPay && x.CreatedOn < vnpExpiry) ||
               (x.SessionState == PaymentSessionState.Creating && x.CreatedOn < stale) ||
               source.Count(p => !p.IsDeleted && p.OrderId == x.OrderId && p.Purpose == x.Purpose && p.Status == PaymentTransactionStatus.Pending) > 1))));
        var total = await AsyncExecuter.LongCountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.CreatedOn).ThenBy(x => x.Id)
            .Skip((page - 1) * take).Take(take).Select(x => new PaymentReconciliationDto
            {
                Id = x.Id, OrderId = x.OrderId, TransactionReference = x.TransactionReference,
                ProviderTransactionId = x.ProviderTransactionId, Amount = x.Amount, Purpose = x.Purpose,
                PaymentMethod = x.PaymentMethod, Status = x.Status, SessionState = x.SessionState,
                Reason = x.ReconciliationReason ?? (x.SessionState == PaymentSessionState.Unknown ? "GatewayResultUnknown" : "PendingSessionRequiresVerification"),
                GatewayVerifiedAt = x.GatewayVerifiedAt, ReconciledAt = x.ReconciledAt,
                RefundRequestId = refunds.Where(r => !r.IsDeleted && r.IsExcessPayment && r.OriginalPaymentTransactionId == x.Id)
                    .Select(r => (Guid?)r.Id).FirstOrDefault()
            }));
        return Success(rows, total, page, take);
    }

    // The caller owns the UoW and the refund-request lock. Match IPN's
    // transaction -> order sequence and never mutate the laundry payment state.
    private async Task<BaseResponse<RefundRequestDto>> ConfirmExcessRefundAsync(RefundRequest request, ConfirmRefundCommand command)
    {
        var reference = await AsyncExecuter.FirstOrDefaultAsync((await _transactionRepo.GetQueryableAsync())
            .Where(x => x.Id == request.OriginalPaymentTransactionId).Select(x => x.TransactionReference));
        var transaction = reference == null ? null : await _transactionRepo.GetForUpdateByReferenceAsync(reference);
        var order = await _orders.GetForUpdateAsync(request.OrderId);
        if (transaction == null || order == null || transaction.OrderId != order.Id ||
            !transaction.RequiresReconciliation || !transaction.GatewayVerifiedAt.HasValue ||
            transaction.Status != PaymentTransactionStatus.RequiresReview)
            return BadRequest<RefundRequestDto>("Không tìm thấy khoản tiền cần đối soát hợp lệ.");
        var refunded = await AsyncExecuter.SumAsync((await _refundRequestRepo.GetQueryableAsync())
            .Where(x => !x.IsDeleted && x.IsExcessPayment && x.OriginalPaymentTransactionId == transaction.Id &&
                x.Status == RefundRequestStatus.Refunded).Select(x => x.Amount));
        if (request.Amount <= 0 || request.Amount > transaction.Amount - refunded || decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest<RefundRequestDto>("Số tiền hoàn vượt khoản tiền dư còn lại của giao dịch.");
        if (refunded + request.Amount == transaction.Amount)
        {
            transaction.Status = PaymentTransactionStatus.Refunded;
            transaction.ReconciledAt = DateTime.UtcNow;
        }
        await _transactionRepo.UpdateAsync(transaction, autoSave: true);
        request.Status = RefundRequestStatus.Refunded;
        request.RefundReference = command.RefundReference!.Trim();
        request.RefundedAt = DateTime.UtcNow;
        request.FinancialSnapshotBasis = "unapplied-receipt";
        request.MerchantIdSnapshot = null;
        request.CommissionRateSnapshot = 0;
        await _refundRequestRepo.UpdateAsync(request, autoSave: true);
        await Audit.LogAsync(new AuditEntry("Refund", "ConfirmExcessRefund", "RefundRequest", request.Id.ToString(), order.OrderCode)
        { Details = new { transaction.Id, transaction.TransactionReference, request.Amount, request.RefundReference } });
        await _notifications.SendAsync([request.CustomerId], NotificationType.RefundConfirmed, "Đã hoàn khoản tiền dư",
            $"Đã hoàn {request.Amount:N0}đ của giao dịch {transaction.TransactionReference}; mã hoàn {request.RefundReference}.", "refund-request", request.Id);
        return Success(ToDto(request), "Đã xác nhận hoàn tiền dư; trạng thái thanh toán đơn được giữ nguyên.");
    }
}
