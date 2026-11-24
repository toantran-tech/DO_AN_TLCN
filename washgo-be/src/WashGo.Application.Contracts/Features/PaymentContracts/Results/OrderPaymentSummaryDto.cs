using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public sealed record PaymentObligationDto(PaymentPurpose Purpose, decimal Due, decimal Paid, decimal Outstanding);
public sealed record PaymentAllocationDto(PaymentPurpose Purpose, decimal CashAmount, long Points);
public sealed record PaymentRefundSummaryDto(decimal PendingAmount, decimal RefundedAmount, long PendingPoints, long RefundedPoints);

public class OrderPaymentSummaryDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public WashOrderStatus OrderStatus { get; set; }
    public string Currency => "VND";
    public List<PaymentObligationDto> Obligations { get; set; } = [];
    public decimal Outstanding { get; set; }
    public bool Settled { get; set; }
    public bool CanRequestStorageReturn { get; set; }
    public string? StorageReturnReason { get; set; }
    public Guid? ReturnLockerId { get; set; }
    public decimal? CancellationBasis { get; set; }
    public decimal? CancellationRatePercent { get; set; }
    public decimal CancellationOffset { get; set; }
    public decimal? CancellationRefundAmount { get; set; }
    public decimal? CancellationRefundRemaining { get; set; }
    public PaymentRefundSummaryDto Refunds { get; set; } = new(0, 0, 0, 0);
    public bool RequiresReconciliation { get; set; }
    public DateTime ComputedAt { get; set; }
    public DateTime QuoteExpiresAt { get; set; }
    public PaymentQuoteDto Quote { get; set; } = new();
}
