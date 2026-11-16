using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class PaymentVerificationDto
{
    public Guid OperationId { get; set; }
    public Guid OrderId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public string State { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public bool RequiresReconciliation { get; set; }
}
