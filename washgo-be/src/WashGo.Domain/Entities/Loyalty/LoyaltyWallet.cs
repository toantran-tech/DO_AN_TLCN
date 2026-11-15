using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Loyalty;

/// <summary>Signed ledger balance: negative means reward debt after a refund, never cash debt.</summary>
public class LoyaltyWallet : Entity<Guid>
{
    public LoyaltyWallet() { }
    public LoyaltyWallet(Guid customerId) : base(customerId) { }
    public long NetPoints { get; set; }
    public long HeldPoints { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
