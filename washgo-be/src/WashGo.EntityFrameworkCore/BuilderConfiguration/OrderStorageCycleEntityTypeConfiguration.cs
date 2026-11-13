using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashOrder;
namespace WashGo.EntityFrameworkCore.BuilderConfiguration;
public class OrderStorageCycleEntityTypeConfiguration : IEntityTypeConfiguration<OrderStorageCycle>
{
    public void Configure(EntityTypeBuilder<OrderStorageCycle> b)
    {
        b.ToTable("order_storage_cycles", "washgo"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ShopAddress).HasMaxLength(1000); b.Property(x => x.Basis).HasMaxLength(50);
        b.Property(x => x.IntakeReceipt).HasMaxLength(120); b.Property(x => x.ExceptionNote).HasMaxLength(500);
        b.Property(x => x.LockerFee).HasPrecision(18,2); b.Property(x => x.WarehouseFee).HasPrecision(18,2);
        b.HasIndex(x => x.OrderId).IsUnique().HasFilter("state IN (1,2,3,4)");
        b.HasIndex(x => new { x.MerchantId, x.State, x.CreatedAt });
        b.HasOne<WashOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
