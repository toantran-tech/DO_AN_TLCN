namespace WashGo.Application.Contracts.Features.LoyaltyContracts.Results;

public class LoyaltyEntryDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long PointsDelta { get; set; }
    public long HeldDelta { get; set; }
    public long NetPointsAfter { get; set; }
    public long HeldPointsAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}
