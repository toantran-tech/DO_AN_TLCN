using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class PaymentSessionDto
{
    public Guid OrderId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public Guid? SessionId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? Amount { get; set; }
    public long? PointsToRedeem { get; set; }
    public PaymentTransactionStatus? TransactionStatus { get; set; }
    public PaymentSessionState? SessionState { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ServerNow { get; set; }
    public bool RequiresReconciliation { get; set; }
    public string? ReasonCode { get; set; }
    public bool CanContinue { get; set; }
    public bool CanVerify { get; set; }
    public bool CanCreateReplacement { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public Guid? VerificationOperationId { get; set; }
    public string? VerificationState { get; set; }
}
