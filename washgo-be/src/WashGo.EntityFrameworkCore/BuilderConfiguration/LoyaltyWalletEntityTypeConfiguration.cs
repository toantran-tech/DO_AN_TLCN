using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Partner;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class LoyaltyWalletEntityTypeConfiguration : IEntityTypeConfiguration<LoyaltyWallet>
{
    public void Configure(EntityTypeBuilder<LoyaltyWallet> builder)
    {
        builder.ToTable("loyalty_wallets", "washgo", t => t.HasCheckConstraint("ck_loyalty_held", "held_points >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasOne<Customer>().WithOne().HasForeignKey<LoyaltyWallet>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}
