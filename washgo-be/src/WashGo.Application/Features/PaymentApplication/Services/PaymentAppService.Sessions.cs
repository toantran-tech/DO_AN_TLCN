using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Uow;
using Volo.Abp.Domain.Repositories;
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
        if (!CurrentUser.IsInRole("Customer"))
            return new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được tạo phiên thanh toán." };
        if (command.PointsToRedeem < 0 || purpose is not (PaymentPurpose.Order or PaymentPurpose.Combined) && command.PointsToRedeem != 0)
            return BadRequest<PaymentResultDto>("Điểm chỉ dùng cho tiền giặt; số điểm không được âm.");
        if (command.PaymentMethod is not (PaymentMethod.VnPay or PaymentMethod.Momo))
            return BadRequest<PaymentResultDto>("Chỉ hỗ trợ thanh toán trực tuyến qua VNPay hoặc MoMo.");

        string reference;
        string orderCode;
        decimal amount;
        DateTime gatewayCreatedAt;
        using (var prepare = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var order = await _orders.GetForUpdateAsync(command.OrderId);
            if (order == null || order.IsDeleted || order.CustomerId != CurrentUser.Id.Value)
                return NotFound<PaymentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            var combined = purpose == PaymentPurpose.Combined;
            var summary = combined ? await BuildObligationsAsync(order, DateTime.UtcNow) : null;
            var due = combined ? (Amount: summary!.Outstanding, Error: (string?)null) : await PaymentDueAsync(order, purpose);
            amount = due.Amount;
            orderCode = order.OrderCode;
            // Do not take transaction locks after the order lock. All creators hold
            // this order lock; callbacks/finalizers consistently lock transaction -> order.
            var blockers = await BlockersAsync(order.Id, purpose, includeOverlaps: true);
            if (summary != null && amount == 0m)
                return Success(new PaymentResultDto { OrderId = order.Id, OrderCode = orderCode,
                    Purpose = purpose, PaymentStatus = order.PaymentStatus,
                    Summary = await BuildOrderPaymentSummaryAsync(order) });
            var view = blockers.Count == 0 ? null : PaymentSessionRules.View(order.Id, blockers[0].Purpose, blockers, DateTime.UtcNow, UseLiveGateway);
            if (blockers.Any(x => x.RequiresReconciliation))
                return SessionConflict("PaymentRequiresReview", "Có khoản tiền cần đối soát; chưa tạo giao dịch mới.", view!);
            var pending = blockers.Where(x => x.Status == PaymentTransactionStatus.Pending).ToList();
            if (pending.Count > 1)
                return SessionConflict("LegacyPaymentsRequireReview", "Đơn có nhiều giao dịch cũ đang chờ; cần đối soát trước khi tạo URL mới.", view!);
            if (pending.Count == 1)
            {
                var existing = pending[0];
                if (existing.Purpose != purpose)
                    return SessionConflict("OverlappingPaymentSession", "Đơn có phiên thanh toán cần tiếp tục hoặc xác minh trước.", view!);
                if (due.Error != null)
                    return SessionConflict("PaymentStateChanged", "Đơn đã thay đổi; cần xác minh phiên hiện tại.", view!);
                if (existing.PointsToRedeem != command.PointsToRedeem)
                    return SessionConflict("PaymentPointsConflict", "Phiên hiện tại đang dùng số điểm khác; không tạo phiên thay thế khi chưa xác minh.", view!);
                if (existing.PaymentMethod != command.PaymentMethod)
                    return SessionConflict("PaymentMethodConflict", "Đã có phiên thanh toán bằng phương thức khác. Vui lòng dùng phiên hiện tại hoặc chờ xác minh.", view!);
                if (summary != null ? !CombinedAllocationApplies(order, existing, summary)
                    : existing.Amount + existing.PointsToRedeem != amount)
                    return SessionConflict("PaymentAmountChanged", "Số tiền phải trả đã thay đổi; cần xác minh phiên trước khi tạo giao dịch mới.", view!);
                if (existing.SessionState == PaymentSessionState.Unknown)
                    return SessionConflict("PaymentVerificationRequired", "Kết quả tạo phiên chưa xác định. Không tạo thêm giao dịch; vui lòng chờ xác minh.", view!);
                if (string.IsNullOrWhiteSpace(existing.PaymentUrl))
                    return SessionConflict(existing.CreatedOn < DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds()
                        ? "PaymentVerificationRequired" : "PaymentInitializing", "Phiên đang khởi tạo hoặc cần xác minh. Vui lòng tải lại sau.", view!);
                // VNPay URLs explicitly include this expiry. Expired locally does not
                // prove no money was received; retain the reference for reconciliation.
                if (existing.PaymentMethod == PaymentMethod.VnPay && UseLiveGateway &&
                    (view!.ExpiresAt.HasValue ? view.ExpiresAt <= DateTime.UtcNow : existing.CreatedOn < DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds()))
                    return SessionConflict("PaymentVerificationRequired", "URL VNPay đã quá thời hạn; cần xác minh giao dịch trước khi tạo phiên thay thế.", view!);
                return new BaseResponse<PaymentResultDto>
                {
                    Succeeded = true, StatusCode = HttpStatusCode.OK,
                    Data = PaymentView(order, existing), Message = "Sử dụng lại phiên thanh toán hiện tại.",
                    Meta = new { code = "PaymentSessionReused", reused = true }
                };
            }

            if (due.Error != null) return BadRequest<PaymentResultDto>(due.Error);
            var quote = await BuildQuoteAsync(order, purpose, command.PointsToRedeem, [], summary);
            if (combined && !MatchesCombinedQuote(command.ExpectedQuoteToken, quote.QuoteToken))
                return new()
                {
                    StatusCode = HttpStatusCode.Conflict,
                    Message = "Bảng kê đã đổi hoặc hết hạn. Vui lòng xác nhận lại.",
                    Data = new PaymentResultDto
                    {
                        OrderId = order.Id, OrderCode = orderCode, Purpose = purpose, PaymentStatus = order.PaymentStatus,
                        Summary = await BuildOrderPaymentSummaryAsync(order, command.PointsToRedeem)
                    },
                    Meta = new { code = "QuoteChanged" }
                };
            if (!quote.CanCreate)
                return QuoteError(quote.ReasonCode ?? "PaymentNotEligible", quote,
                    quote.ReasonCode == "InsufficientPoints" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
            var selected = quote.Methods.Single(x => x.PaymentMethod == command.PaymentMethod);
            if (!selected.Enabled) return QuoteError(selected.ReasonCode ?? "PaymentMethodUnavailable", quote, HttpStatusCode.BadRequest);
            if (!combined && command.ExpectedQuoteToken != null && command.ExpectedQuoteToken != quote.QuoteToken)
                return QuoteError("QuoteChanged", quote, HttpStatusCode.Conflict);
            if (string.IsNullOrWhiteSpace(command.ExpectedQuoteToken) && await _transactionRepo.AnyAsync(x =>
                x.OrderId == order.Id && x.Purpose == purpose && x.VerifiedClosedAt != null && !x.IsDeleted))
                return QuoteError("QuoteConfirmationRequired", quote, HttpStatusCode.Conflict);
            reference = NewReference(purpose switch
            {
                PaymentPurpose.OverdueStorageFee => "FEE", PaymentPurpose.CancellationFee => "CANCEL",
                PaymentPurpose.Combined => "TOTAL", _ => "PAY"
            }, order.OrderCode, command.PaymentMethod);
            var laundry = summary == null ? (purpose == PaymentPurpose.Order ? amount : 0m) : OutstandingOf(summary, PaymentPurpose.Order);
            var transactionToCreate = new PaymentTransaction
            {
                OrderId = order.Id, TransactionReference = reference, PaymentMethod = command.PaymentMethod,
                Purpose = purpose, Amount = amount, Status = PaymentTransactionStatus.Pending,
                SessionState = PaymentSessionState.Creating, IsActiveSession = true,
                MerchantIdSnapshot = order.MerchantId, CommissionRateSnapshot = order.CommissionRateApplied,
                FinancialSnapshotBasis = "payment-created",
                LoyaltyVersion = laundry > 0m ? 1 : 0,
                PointsToRedeem = command.PointsToRedeem,
                LaundryAmount = laundry,
                StorageAmount = summary == null ? 0m : OutstandingOf(summary, PaymentPurpose.OverdueStorageFee),
                CancellationAmount = summary == null ? 0m : OutstandingOf(summary, PaymentPurpose.CancellationFee),
                LoyaltyEarnPercent = quote.EarnPercent,
                LoyaltyMaxUsePercent = quote.MaxUsePercent
            };
            gatewayCreatedAt = DateTime.UtcNow;
            transactionToCreate.GatewayCreatedAt = gatewayCreatedAt;
            if (command.PaymentMethod == PaymentMethod.VnPay) transactionToCreate.GatewayExpiresAt = transactionToCreate.GatewayCreatedAt.Value.AddMinutes(15);
            transactionToCreate.AssignIdIfEmptyForBulkInsert();
            transactionToCreate.Amount = amount - command.PointsToRedeem;
            if (!await Loyalty.HoldAsync(transactionToCreate, order.CustomerId))
            {
                await prepare.RollbackAsync();
                return SessionConflict("InsufficientPoints", "Điểm khả dụng đã thay đổi; vui lòng tải lại báo giá.");
            }
            amount = transactionToCreate.Amount;
            await _transactionRepo.InsertAsync(transactionToCreate, autoSave: true);
            await prepare.CompleteAsync();
        }

        // The reference is durable before HTTP. Never retry a gateway POST under a new
        // reference after a timeout; a crash leaves Creating and blocks another session.
        var result = await GatewayUrlAsync(command.OrderId, command.PaymentMethod, reference,
            orderCode, amount, command.ClientIpAddress, gatewayCreatedAt);
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
            if (!result.IsUncertain) await Loyalty.ReleaseAsync(transaction, current.CustomerId);
            await _transactionRepo.UpdateAsync(transaction, autoSave: true);
            await finalize.CompleteAsync();
            return result.IsUncertain
                ? SessionConflict("PaymentVerificationRequired", "Chưa xác định kết quả từ cổng. Không tạo lại giao dịch; vui lòng chờ xác minh.")
                : BadRequest<PaymentResultDto>(result.Error ?? "Cổng từ chối tạo phiên thanh toán.");
        }
        transaction.PaymentUrl = result.Url;
        transaction.SessionState = PaymentSessionState.Ready;
        await _transactionRepo.UpdateAsync(transaction, autoSave: true);
        var stillApplicable = purpose == PaymentPurpose.Combined
            ? CombinedAllocationApplies(current, transaction, await BuildObligationsAsync(current, DateTime.UtcNow))
            : await LegacyPaymentAmountMatchesAsync(current, purpose, amount + transaction.PointsToRedeem);
        await finalize.CompleteAsync();
        if (!stillApplicable)
            return SessionConflict("PaymentStateChanged", "Đơn đã thay đổi trong lúc tạo phiên; giao dịch được giữ lại để xác minh, không mở URL mới.");
        return Success(PaymentView(current, transaction), "Đã tạo phiên thanh toán.");
    }

    private async Task<bool> LegacyPaymentAmountMatchesAsync(WashOrder order, PaymentPurpose purpose, decimal amount)
    {
        var due = await PaymentDueAsync(order, purpose);
        return due.Error == null && due.Amount == amount;
    }

    private async Task<(decimal Amount, string? Error)> PaymentDueAsync(WashOrder order, PaymentPurpose purpose)
    {
        if (purpose == PaymentPurpose.Combined)
        {
            var summary = await BuildObligationsAsync(order, DateTime.UtcNow);
            return (summary.Outstanding, summary.Outstanding > 0m ? null : "Đơn chưa có khoản cần thu.");
        }
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
        OrderId = order.Id, OrderCode = order.OrderCode, Amount = transaction.Amount, SessionId = transaction.Id,
        Purpose = transaction.Purpose, PaymentMethod = transaction.PaymentMethod, PaymentStatus = order.PaymentStatus,
        TransactionReference = transaction.TransactionReference, PaymentUrl = transaction.PaymentUrl ?? string.Empty,
        TransactionStatus = transaction.Status, SessionState = transaction.SessionState,
        RequiresReconciliation = transaction.RequiresReconciliation,
        LaundryAmount = transaction.LaundryAmount, PointsToRedeem = transaction.PointsToRedeem,
        PointsHeld = transaction.PointsHeld, PointsEarned = transaction.PointsEarned
    };
}
