using System.Net;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService
{
    private PaymentCapabilities Capabilities => LazyServiceProvider.LazyGetRequiredService<PaymentCapabilities>();
    private static bool ValidPurpose(PaymentPurpose purpose) => purpose is PaymentPurpose.Order or PaymentPurpose.CancellationFee or PaymentPurpose.OverdueStorageFee;
    private bool CustomerSignedIn => CurrentUser.Id.HasValue && CurrentUser.IsInRole("Customer");

    private async Task<List<PaymentTransaction>> BlockersAsync(Guid orderId, PaymentPurpose purpose) =>
        (await _transactionRepo.GetListAsync(x => x.OrderId == orderId && x.Purpose == purpose && !x.IsDeleted &&
            (x.Status == PaymentTransactionStatus.Pending || x.RequiresReconciliation && x.ReconciledAt == null)))
        .OrderBy(x => x.CreatedOn).ThenBy(x => x.Id).ToList();

    public async Task<BaseResponse<PaymentSessionDto?>> GetActiveSessionAsync(Guid orderId, PaymentPurpose purpose)
    {
        if (!CustomerSignedIn) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được xem phiên của mình." };
        if (!ValidPurpose(purpose)) return BadRequest<PaymentSessionDto?>("purpose phải là 1, 2 hoặc 3.");
        var order = await _orders.FindAsync(orderId);
        if (order == null || order.CustomerId != CurrentUser.Id) return NotFound<PaymentSessionDto?>("Không tìm thấy đơn hàng của bạn.");
        var blockers = await BlockersAsync(orderId, purpose);
        if (blockers.Count == 0) return Success<PaymentSessionDto?>(null);
        var view = PaymentSessionRules.View(orderId, purpose, blockers, DateTime.UtcNow, UseLiveGateway);
        if ((await PaymentDueAsync(order, purpose)).Error != null) view.CanContinue = false;
        await EnrichVerificationAsync(view);
        return Success<PaymentSessionDto?>(view);
    }

    public async Task<BaseResponse<PaymentQuoteDto>> QuotePaymentAsync(Guid orderId, PaymentPurpose purpose, long pointsToRedeem)
    {
        if (!CustomerSignedIn) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được xem báo phí của mình." };
        if (!ValidPurpose(purpose) || pointsToRedeem < 0 || purpose != PaymentPurpose.Order && pointsToRedeem != 0)
            return BadRequest<PaymentQuoteDto>("purpose hoặc số điểm không hợp lệ; điểm chỉ dùng cho tiền giặt.");
        var order = await _orders.FindAsync(orderId);
        if (order == null || order.CustomerId != CurrentUser.Id) return NotFound<PaymentQuoteDto>("Không tìm thấy đơn hàng của bạn.");
        return Success(await BuildQuoteAsync(order, purpose, pointsToRedeem, await BlockersAsync(orderId, purpose)));
    }

    private async Task<PaymentQuoteDto> BuildQuoteAsync(WashOrder order, PaymentPurpose purpose, long points,
        List<PaymentTransaction> blockers)
    {
        var due = await PaymentDueAsync(order, purpose);
        var policy = await _settings.GetAsync();
        long available = 0;
        if (purpose == PaymentPurpose.Order)
        {
            // Pure reads: unlike opening the wallet, a quote never writes an Opening entry.
            var wallet = await LazyServiceProvider.LazyGetRequiredService<IRepository<LoyaltyWallet, Guid>>().FindAsync(order.CustomerId);
            available = wallet != null ? LoyaltyMath.Available(wallet.NetPoints, wallet.HeldPoints) :
                Math.Max(0, (await LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>().FindAsync(order.CustomerId))?.LoyaltyPoints ?? 0);
        }
        var maximum = purpose == PaymentPurpose.Order ? LoyaltyMath.MaximumUse(Math.Max(0, due.Amount), policy.LoyaltyMaxUsePercent, available) : 0;
        var quote = new PaymentQuoteDto
        {
            OrderId = order.Id, Purpose = purpose, ComputedAt = DateTime.UtcNow, GrossDue = Math.Max(0, due.Amount),
            PointsToRedeem = points, AvailablePoints = available, MaximumPoints = maximum,
            CashAmount = Math.Max(0, due.Amount - points), Message = due.Error,
            EarnPercent = purpose == PaymentPurpose.Order ? policy.LoyaltyEarnPercent : 0,
            MaxUsePercent = purpose == PaymentPurpose.Order ? policy.LoyaltyMaxUsePercent : 0
        };
        quote.Methods = Capabilities.For(quote.CashAmount);
        if (blockers.Count > 0)
        {
            quote.ActiveSession = PaymentSessionRules.View(order.Id, purpose, blockers, quote.ComputedAt, UseLiveGateway);
            if (due.Error != null) quote.ActiveSession.CanContinue = false;
            await EnrichVerificationAsync(quote.ActiveSession);
            quote.ReasonCode = quote.ActiveSession.ReasonCode;
            foreach (var method in quote.Methods) { method.Enabled = false; method.ReasonCode = "ExistingSessionTakesPriority"; }
        }
        else if (due.Error != null) quote.ReasonCode = "PaymentNotPayable";
        else if (purpose == PaymentPurpose.Order && points > LoyaltyMath.MaximumUse(Math.Max(0, due.Amount), policy.LoyaltyMaxUsePercent, long.MaxValue))
            quote.ReasonCode = "PointsLimitExceeded";
        else if (points > maximum) quote.ReasonCode = "InsufficientPoints";
        else if (!quote.Methods.Any(x => x.Enabled))
        {
            quote.ReasonCode = "NoEligiblePaymentMethod";
            quote.Message = "Khoản phải trả chưa đáp ứng hạn mức hoặc cấu hình cổng. Vui lòng liên hệ WashGo; hệ thống không tự tăng, miễn phí hoặc xác nhận đã thanh toán.";
        }
        quote.CanCreate = quote.ReasonCode == null;
        quote.RecommendedMethod = quote.Methods.FirstOrDefault(x => x.Enabled)?.PaymentMethod;
        var lastClosed = await AsyncExecuter.FirstOrDefaultAsync((await _transactionRepo.GetQueryableAsync())
            .Where(x => x.OrderId == order.Id && x.Purpose == purpose && !x.IsDeleted && x.VerifiedClosedAt != null)
            .OrderByDescending(x => x.VerifiedClosedAt).ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.VerifiedClosedAt }));
        quote.QuoteToken = AdministrativeWriteGuard.Version(new
        {
            order.Id, purpose, order.Status, order.PaymentStatus, quote.GrossDue, points, quote.CashAmount,
            quote.EarnPercent, quote.MaxUsePercent, lastClosed,
            methods = quote.Methods.Select(x => new { x.PaymentMethod, x.MinAmount, x.MaxAmount, x.LimitSource })
        });
        return quote;
    }

    private static BaseResponse<PaymentResultDto> SessionConflict(string code, string message, PaymentSessionDto session) => new()
    {
        StatusCode = HttpStatusCode.Conflict, Message = message,
        Meta = new { code, orderId = session.OrderId, purpose = session.Purpose, existingPaymentMethod = session.PaymentMethod,
            session.SessionState, session.RequiresReconciliation, session.RetryAfterSeconds, activeSession = session }
    };

    private async Task EnrichVerificationAsync(PaymentSessionDto session)
    {
        if (!session.SessionId.HasValue) return;
        var latest = await AsyncExecuter.FirstOrDefaultAsync((await Verifications.GetQueryableAsync())
            .Where(x => x.TransactionId == session.SessionId && x.ParentOperationId == null)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));
        if (latest == null) return;
        var verification = await VerificationViewAsync(latest);
        session.VerificationOperationId = latest.Id;
        session.VerificationState = verification.State;
        if (verification.State is "Queued" or "Verifying")
        {
            session.CanContinue = false; session.CanVerify = false;
            session.RetryAfterSeconds = verification.RetryAfterSeconds;
            session.ReasonCode = "PaymentVerificationInProgress";
        }
    }

    private static BaseResponse<PaymentResultDto> QuoteError(string code, PaymentQuoteDto quote, HttpStatusCode status) => new()
    {
        StatusCode = status, Message = quote.Message ?? "Vui lòng tải báo phí và xác nhận số tiền/phương thức trước khi thanh toán.",
        Meta = new { code, quote }
    };
}
