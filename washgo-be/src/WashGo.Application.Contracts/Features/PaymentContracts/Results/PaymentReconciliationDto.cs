using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class PaymentReconciliationDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public PaymentTransactionStatus Status { get; set; }
    public PaymentSessionState SessionState { get; set; }
    public string? Reason { get; set; }
    public DateTime? GatewayVerifiedAt { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Guid? RefundRequestId { get; set; }
}
