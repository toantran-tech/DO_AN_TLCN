using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Loyalty;

public class LoyaltyEntry : Entity<Guid>
{
    public LoyaltyEntry() { }
    public LoyaltyEntry(Guid id) : base(id) { }
    public Guid CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public Guid? RefundRequestId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public long PointsDelta { get; set; }
    public long HeldDelta { get; set; }
    public long BalanceAfter { get; set; }
    public long HeldAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
