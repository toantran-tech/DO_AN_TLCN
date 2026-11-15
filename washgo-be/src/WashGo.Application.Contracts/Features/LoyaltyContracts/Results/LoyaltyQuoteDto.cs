namespace WashGo.Application.Contracts.Features.LoyaltyContracts.Results;

public class LoyaltyQuoteDto
{
    public Guid OrderId { get; set; }
    public decimal LaundryAmount { get; set; }
    public long AvailablePoints { get; set; }
    public long MaximumPoints { get; set; }
    public long PointsToRedeem { get; set; }
    public decimal CashAmount { get; set; }
    public long ExpectedEarnedPoints { get; set; }
    public bool IsEstimate => true;
}
