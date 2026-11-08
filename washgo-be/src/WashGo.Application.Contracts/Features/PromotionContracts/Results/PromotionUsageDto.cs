namespace WashGo.Application.Contracts.Features.PromotionContracts.Results;

public class PromotionUsageDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal OrderAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime UsedAt { get; set; }
    public bool IsReverted { get; set; }
    public DateTime? RevertedAt { get; set; }
    // Old usage records never stored a reason. Do not invent historical evidence.
    public string? RevertReason { get; set; }
}
