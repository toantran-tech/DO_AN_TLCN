using System.Globalization;
using System.Net;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public static class PaymentSessionRules
{
    public static bool BlocksNewPayment(PaymentTransaction t) => !t.IsDeleted &&
        (t.Status == PaymentTransactionStatus.Pending || t.RequiresReconciliation && !t.ReconciledAt.HasValue);

    public static DateTime? GatewayDate(PaymentTransaction transaction, string key)
    {
        if (!Uri.TryCreate(transaction.PaymentUrl, UriKind.Absolute, out var uri)) return null;
        var value = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2))
            .FirstOrDefault(x => x.Length == 2 && x[0] == key)?[1];
        return DateTime.TryParseExact(WebUtility.UrlDecode(value), "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime : null;
    }

    public static PaymentSessionDto View(Guid orderId, PaymentPurpose purpose, IReadOnlyCollection<PaymentTransaction> blockers,
        DateTime now, bool live)
    {
        var dto = new PaymentSessionDto { OrderId = orderId, Purpose = purpose, ServerNow = now };
        if (blockers.Count != 1)
        {
            dto.RequiresReconciliation = true;
            dto.ReasonCode = "MultipleSessionsRequireReview";
            return dto;
        }
        var t = blockers.Single();
        dto.SessionId = t.Id; dto.PaymentMethod = t.PaymentMethod; dto.Amount = t.Amount;
        dto.PointsToRedeem = t.PointsToRedeem; dto.TransactionStatus = t.Status; dto.SessionState = t.SessionState;
        dto.ExpiresAt = t.GatewayExpiresAt ?? (t.PaymentMethod == PaymentMethod.VnPay ? GatewayDate(t, "vnp_ExpireDate") : null);
        dto.RequiresReconciliation = t.RequiresReconciliation;
        var creating = t.SessionState == PaymentSessionState.Creating &&
            t.CreatedOn >= new DateTimeOffset(now.AddMinutes(-2)).ToUnixTimeSeconds();
        var expired = live && t.PaymentMethod == PaymentMethod.VnPay &&
            (dto.ExpiresAt.HasValue ? dto.ExpiresAt <= now : t.CreatedOn < new DateTimeOffset(now.AddMinutes(-15)).ToUnixTimeSeconds());
        dto.CanContinue = !t.RequiresReconciliation && t.SessionState == PaymentSessionState.Ready &&
            !string.IsNullOrWhiteSpace(t.PaymentUrl) && !expired;
        dto.CanVerify = !t.RequiresReconciliation && !creating;
        dto.ReasonCode = t.RequiresReconciliation ? "PaymentRequiresReview" : creating ? "PaymentInitializing" :
            dto.CanContinue ? "PaymentSessionReady" : expired ? "PaymentExpiredRequiresVerification" : "PaymentVerificationRequired";
        dto.RetryAfterSeconds = creating ? 2 : dto.CanVerify ? 10 : null;
        // Replacing a pending/uncertain session is never permitted by a local clock.
        dto.CanCreateReplacement = false;
        return dto;
    }
}
