using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

/// <summary>
/// Authoritative result of one payment attempt for the Customer's order and purpose (GET /payment/orders/{orderId}/result).
/// Built only from server state (verified IPN/query), never from browser query strings.
/// </summary>
public class PaymentOutcomeDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; }
    /// <summary>Opaque id of the attempt (payment session); null when no attempt exists.</summary>
    public Guid? SessionId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    /// <summary>Succeeded | Failed | Cancelled | Pending | RequiresReview | Refunded | NotStarted.</summary>
    public string Status { get; set; } = "NotStarted";
    /// <summary>FE result page: success | failed | cancelled; null while unresolved or without an attempt.</summary>
    public string? ResultPage { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    /// <summary>True when the outcome comes from a verified gateway result (IPN or query), not from a redirect.</summary>
    public bool Verified { get; set; }
    /// <summary>Amount charged through the gateway for this attempt.</summary>
    public decimal? Amount { get; set; }
    public List<PaymentAllocationDto> Allocations { get; set; } = [];
    public OrderPaymentSummaryDto? Summary { get; set; }
    public long PointsRedeemed { get; set; }
    /// <summary>The obligation of this purpose is paid (laundry Paid; storage fee or cancellation fee has nothing outstanding).</summary>
    public bool ObligationSettled { get; set; }
    public DateTime? RecordedAt { get; set; }
    public DateTime ServerNow { get; set; }
    public int? RetryAfterSeconds { get; set; }
    /// <summary>A newer attempt exists for the same order and purpose.</summary>
    public bool SupersededByNewerSession { get; set; }
    public Guid? VerificationOperationId { get; set; }
    public string? VerificationState { get; set; }
    public PaymentOutcomeCapabilitiesDto Capabilities { get; set; } = new();
}

public class PaymentOutcomeCapabilitiesDto
{
    /// <summary>POST verify-session is useful (still pending, not being created).</summary>
    public bool CanVerify { get; set; }
    /// <summary>The existing gateway session can still be continued (read the URL from active-session).</summary>
    public bool CanContinue { get; set; }
    /// <summary>A new payment may be created: the attempt is closed and the obligation is still due.</summary>
    public bool CanRetry { get; set; }
    public bool CanViewOrder { get; set; } = true;
}
