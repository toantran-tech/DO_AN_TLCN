using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashOrder;
namespace WashGo.EntityFrameworkCore.BuilderConfiguration;
public class StorageReturnRequestEntityTypeConfiguration : IEntityTypeConfiguration<StorageReturnRequest>
{
    public void Configure(EntityTypeBuilder<StorageReturnRequest> b)
    {
        b.ToTable("storage_return_requests", "washgo"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.AcceptedFeeTotal).HasPrecision(18,2);
        b.HasIndex(x => x.CycleId).IsUnique().HasFilter("state IN (1,2,3)");
        b.HasIndex(x => new { x.OrderId, x.RequestedAt });
        b.HasOne<OrderStorageCycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
    }
}
