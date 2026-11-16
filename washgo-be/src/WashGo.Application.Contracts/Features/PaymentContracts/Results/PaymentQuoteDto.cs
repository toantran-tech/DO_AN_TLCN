using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class PaymentQuoteDto
{
    public Guid OrderId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public DateTime ComputedAt { get; set; }
    public decimal GrossDue { get; set; }
    public long PointsToRedeem { get; set; }
    public long AvailablePoints { get; set; }
    public long MaximumPoints { get; set; }
    public decimal CashAmount { get; set; }
    public decimal EarnPercent { get; set; }
    public decimal MaxUsePercent { get; set; }
    public string QuoteToken { get; set; } = string.Empty;
    public bool CanCreate { get; set; }
    public string? ReasonCode { get; set; }
    public string? Message { get; set; }
    public PaymentMethod? RecommendedMethod { get; set; }
    public PaymentSessionDto? ActiveSession { get; set; }
    public List<PaymentMethodCapabilityDto> Methods { get; set; } = [];
}
