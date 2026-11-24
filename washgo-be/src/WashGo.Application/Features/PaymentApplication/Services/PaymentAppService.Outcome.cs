using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
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
    /// <summary>
    /// Browser return from VNPay/MoMo. Only a correctly signed return identifies the attempt; the page is then chosen from
    /// the stored, verified state (IPN/query). A still-pending attempt returns to the order and queues a gateway query. Returns the FE URL to redirect to, or null when the FE origin is not configured safely.
    /// </summary>
    public async Task<string?> ResolveReturnRedirectAsync(PaymentMethod method, IReadOnlyDictionary<string, string> parameters)
    {
        var frontend = FrontendOrigin();
        if (frontend == null) return null;
        string? reference = null;
        long? amount = null;
        if (method == PaymentMethod.VnPay)
        {
            var secret = _configuration["Payment:VnPay:HashSecret"];
            if (!string.IsNullOrWhiteSpace(secret) && VerifyVnPaySignature(parameters, secret) &&
                parameters.GetValueOrDefault("vnp_TmnCode") == _configuration["Payment:VnPay:TmnCode"] &&
                long.TryParse(parameters.GetValueOrDefault("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var cents))
            {
                reference = parameters.GetValueOrDefault("vnp_TxnRef");
                amount = cents;
            }
        }
        else if (method == PaymentMethod.Momo && MomoReturn(parameters) is { } ret && _momo.VerifyIpn(ret))
        {
            reference = ret.OrderId;
            amount = ret.Amount;
        }
        if (string.IsNullOrWhiteSpace(reference))
        {
            Logger.LogWarning("{Method} return rejected: missing or invalid signature.", method);
            return $"{frontend}/orders";
        }

        var transaction = await _transactionRepo.FirstOrDefaultAsync(x => x.TransactionReference == reference && !x.IsDeleted);
        if (transaction == null || transaction.PaymentMethod != method)
            return $"{frontend}/orders";
        var order = await _orders.FindAsync(transaction.OrderId);
        if (order == null)
            return $"{frontend}/orders";
        var expected = method == PaymentMethod.VnPay
            ? decimal.ToInt64(decimal.Round(transaction.Amount * 100m, 0, MidpointRounding.AwayFromZero))
            : decimal.ToInt64(decimal.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero));
        var page = amount != expected ? PaymentOutcomeRules.ReviewPage : PaymentOutcomeRules.Classify(transaction, order).Page;
        if (page == PaymentOutcomeRules.PendingPage)
            await EnsureVerificationQueuedAsync(transaction, order);
        var path = page is PaymentOutcomeRules.PendingPage or PaymentOutcomeRules.ReviewPage
            ? $"/orders/{order.Id}" : $"/payment/{page}";
        return $"{frontend}{path}?orderId={order.Id}&purpose={(int)transaction.Purpose}&sessionId={transaction.Id}";
    }

    /// <summary>Customer read of one attempt (sessionId) or the latest attempt for the order and purpose. Never changes data.</summary>
    public async Task<BaseResponse<PaymentOutcomeDto>> GetOutcomeAsync(Guid orderId, PaymentPurpose purpose, Guid? sessionId)
    {
        if (!CustomerSignedIn) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được xem kết quả thanh toán của mình." };
        if (!ValidPurpose(purpose)) return new() { StatusCode = HttpStatusCode.BadRequest, Message = "purpose phải là 1, 2, 3 hoặc 4.", Meta = new { code = "InvalidPurpose" } };
        var order = await _orders.FindAsync(orderId);
        if (order == null || order.IsDeleted || order.CustomerId != CurrentUser.Id)
            return NotFound<PaymentOutcomeDto>("Không tìm thấy đơn hàng của bạn.");
        var attempts = (await _transactionRepo.GetListAsync(x => x.OrderId == orderId && x.Purpose == purpose && !x.IsDeleted))
            .OrderByDescending(x => x.GatewayCreatedAt ?? DateTimeOffset.FromUnixTimeSeconds(x.CreatedOn).UtcDateTime).ThenByDescending(x => x.CreatedOn).ToList();
        var transaction = sessionId.HasValue ? attempts.FirstOrDefault(x => x.Id == sessionId.Value) : attempts.FirstOrDefault();
        if (sessionId.HasValue && transaction == null)
            return NotFound<PaymentOutcomeDto>("Không tìm thấy lần thanh toán này của đơn.");
        var now = DateTime.UtcNow;
        var dto = new PaymentOutcomeDto
        {
            OrderId = order.Id, OrderCode = order.OrderCode, Purpose = purpose, ServerNow = now,
            Summary = await BuildOrderPaymentSummaryAsync(order)
        };
        dto.ObligationSettled = purpose == PaymentPurpose.Combined
            ? dto.Summary.Settled : await ObligationSettledAsync(order, purpose);
        if (transaction == null)
        {
            dto.ReasonCode = "NoPaymentAttempt";
            dto.Capabilities.CanRetry = !dto.ObligationSettled &&
                (await BlockersAsync(orderId, purpose, includeOverlaps: true)).Count == 0 &&
                (await PaymentDueAsync(order, purpose)).Error == null;
            return Success(dto);
        }
        var (status, page, reason) = PaymentOutcomeRules.Classify(transaction, order);
        dto.SessionId = transaction.Id;
        dto.PaymentMethod = transaction.PaymentMethod;
        dto.Status = status;
        dto.ResultPage = page is PaymentOutcomeRules.PendingPage or PaymentOutcomeRules.ReviewPage ? null : page;
        dto.ReasonCode = reason;
        dto.Verified = transaction.GatewayVerifiedAt.HasValue || transaction.VerifiedClosedAt.HasValue;
        dto.Amount = transaction.Amount;
        if (!transaction.RequiresReconciliation && transaction.Status is PaymentTransactionStatus.Succeeded or PaymentTransactionStatus.Refunded)
            dto.Allocations = DescribeAllocations(transaction);
        dto.PointsRedeemed = transaction.Status == PaymentTransactionStatus.Succeeded ? transaction.PointsToRedeem : 0;
        dto.RecordedAt = transaction.Status == PaymentTransactionStatus.Pending ? null : transaction.CompletedAt;
        dto.SupersededByNewerSession = attempts[0].Id != transaction.Id;
        if (status == "Pending")
        {
            var view = PaymentSessionRules.View(orderId, purpose, [transaction], now, UseLiveGateway);
            var verification = await AsyncExecuter.FirstOrDefaultAsync((await Verifications.GetQueryableAsync())
                .Where(x => x.TransactionId == transaction.Id && x.ParentOperationId == null)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));
            dto.VerificationOperationId = verification?.Id;
            dto.VerificationState = verification?.State;
            var verifying = verification?.IsActive == true;
            dto.Capabilities.CanVerify = view.CanVerify && !verifying;
            dto.Capabilities.CanContinue = view.CanContinue && !verifying &&
                (purpose != PaymentPurpose.Combined || CombinedAllocationApplies(order, transaction, dto.Summary));
            dto.RetryAfterSeconds = verifying
                ? (int)Math.Clamp(Math.Ceiling(((verification!.LeaseUntil ?? verification.NextAttemptAt) - now).TotalSeconds), 2, 120)
                : view.RetryAfterSeconds;
            if (view.ReasonCode is { } sessionReason && dto.ReasonCode == "AwaitingGatewayResult") dto.ReasonCode = sessionReason;
        }
        else if (status is "Failed" or "Cancelled")
        {
            // A new attempt only when nothing else is open or under review for this obligation and it is still due.
            dto.Capabilities.CanRetry = !dto.ObligationSettled && (await BlockersAsync(orderId, purpose, includeOverlaps: true)).Count == 0 &&
                (await PaymentDueAsync(order, purpose)).Error == null;
        }
        return Success(dto);
    }

    private async Task<bool> ObligationSettledAsync(WashOrder order, PaymentPurpose purpose) => purpose switch
    {
        PaymentPurpose.Order => order.PaymentStatus == PaymentStatus.Paid,
        PaymentPurpose.CancellationFee => order.CancelFeeAmount > 0 && order.CancelFeePaidAmount >= order.CancelFeeAmount,
        _ => order.PaymentStatus == PaymentStatus.Paid && OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow,
                 await _settings.ForOrderAsync(order)) - order.StorageFeeTotal <= 0
    };

    /// <summary>Queues one gateway query for a pending attempt the customer just returned from (no duplicate while one is active).</summary>
    private async Task EnsureVerificationQueuedAsync(PaymentTransaction transaction, WashOrder order)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await _orders.AcquireLockAsync($"payment-verify-scope:{order.Id}:{(int)transaction.Purpose}");
        var recent = DateTime.UtcNow.AddSeconds(-60);
        if (await Verifications.AnyAsync(x => x.TransactionId == transaction.Id && (x.IsActive || x.UpdatedAt > recent)))
            return;
        await Verifications.InsertAsync(new PaymentVerification(GuidGenerator.Create())
        {
            CustomerId = order.CustomerId, OrderId = order.Id, Purpose = transaction.Purpose, TransactionId = transaction.Id,
            ReasonCode = "QueuedByGatewayReturn"
        }, autoSave: true);
        await uow.CompleteAsync();
    }

    /// <summary>
    /// FE origin for result pages: Payment:Frontend:BaseUrl, an absolute http(s) origin without path/query, HTTPS unless
    /// loopback, and listed in Payment:Frontend:AllowedOrigins when that list is set. Redirect targets never come from the request.
    /// </summary>
    private string? FrontendOrigin()
    {
        var configured = _configuration["Payment:Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(configured) &&
            string.Equals(_configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            configured = "http://localhost:5173";
        if (!Uri.TryCreate(configured?.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            uri.Scheme == "http" && !uri.IsLoopback)
        {
            Logger.LogError("Payment:Frontend:BaseUrl is missing or unsafe; payment result redirect disabled.");
            return null;
        }
        var origin = uri.GetLeftPart(UriPartial.Authority);
        var allowed = (_configuration["Payment:Frontend:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.TrimEnd('/')).ToList();
        if (allowed.Count > 0 && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            Logger.LogError("Payment:Frontend:BaseUrl is not in Payment:Frontend:AllowedOrigins; payment result redirect disabled.");
            return null;
        }
        return origin;
    }

    private static MomoIpnCommand? MomoReturn(IReadOnlyDictionary<string, string> p)
    {
        if (!long.TryParse(p.GetValueOrDefault("amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            !long.TryParse(p.GetValueOrDefault("transId", "0"), NumberStyles.None, CultureInfo.InvariantCulture, out var transId) ||
            !int.TryParse(p.GetValueOrDefault("resultCode"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var resultCode) ||
            !long.TryParse(p.GetValueOrDefault("responseTime", "0"), NumberStyles.None, CultureInfo.InvariantCulture, out var responseTime))
            return null;
        return new MomoIpnCommand
        {
            PartnerCode = p.GetValueOrDefault("partnerCode", ""), OrderId = p.GetValueOrDefault("orderId", ""),
            RequestId = p.GetValueOrDefault("requestId", ""), Amount = amount, OrderInfo = p.GetValueOrDefault("orderInfo", ""),
            OrderType = p.GetValueOrDefault("orderType", ""), TransId = transId, ResultCode = resultCode,
            Message = p.GetValueOrDefault("message", ""), PayType = p.GetValueOrDefault("payType", ""),
            ResponseTime = responseTime, ExtraData = p.GetValueOrDefault("extraData", ""), Signature = p.GetValueOrDefault("signature", "")
        };
    }
}
