using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Loyalty;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class LoyaltyEntryEntityTypeConfiguration : IEntityTypeConfiguration<LoyaltyEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyEntry> builder)
    {
        builder.ToTable("loyalty_entries", "washgo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EventKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.EventKey).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt, x.Id });
        builder.HasOne<LoyaltyWallet>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
