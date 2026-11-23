using System.Text.RegularExpressions;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

/// <summary>
/// Maps a payment attempt to the result shown to the Customer. Same rules for VNPay and MoMo:
/// success only after a verified result was applied to the right obligation; anything unverified stays pending.
/// </summary>
public static class PaymentOutcomeRules
{
    public const string SuccessPage = "success", FailedPage = "failed", CancelledPage = "cancelled", PendingPage = "pending", ReviewPage = "review";

    // VNPay 24 = customer cancelled at the gateway; MoMo 1006 = user denied/cancelled the payment.
    private static readonly Regex CustomerCancel = new(@"^(VNPay response=24\b|MoMo resultCode=1006\b)", RegexOptions.CultureInvariant);

    public static bool IsCustomerCancellation(string? failureReason) => failureReason != null && CustomerCancel.IsMatch(failureReason);

    public static (string Status, string Page, string ReasonCode) Classify(PaymentTransaction t, WashOrder? order)
    {
        if (t.Status == PaymentTransactionStatus.RequiresReview || t.RequiresReconciliation && !t.ReconciledAt.HasValue)
            return ("RequiresReview", ReviewPage, t.ReconciliationReason ?? "PaymentRequiresReview");
        switch (t.Status)
        {
            case PaymentTransactionStatus.Succeeded:
                // Laundry success must agree with the order being Paid; otherwise it is a reconciliation case, not success.
                if (t.Purpose == PaymentPurpose.Order && order?.PaymentStatus != PaymentStatus.Paid)
                    return ("RequiresReview", ReviewPage, "ObligationNotApplied");
                return ("Succeeded", SuccessPage, "PaymentRecorded");
            case PaymentTransactionStatus.Refunded:
                return ("Refunded", ReviewPage, "PaymentRefunded");
            case PaymentTransactionStatus.Failed:
                if (!t.VerifiedClosedAt.HasValue) return ("Failed", FailedPage, "SessionNotCreated");
                return IsCustomerCancellation(t.FailureReason)
                    ? ("Cancelled", CancelledPage, "CustomerCancelled")
                    : ("Failed", FailedPage, "GatewayDeclined");
            default:
                return ("Pending", PendingPage, t.SessionState == PaymentSessionState.Unknown ? "PaymentVerificationRequired" : "AwaitingGatewayResult");
        }
    }
}
