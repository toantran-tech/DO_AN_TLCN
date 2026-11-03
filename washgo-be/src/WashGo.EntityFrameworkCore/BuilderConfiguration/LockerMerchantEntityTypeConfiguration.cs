using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class LockerMerchantEntityTypeConfiguration : IEntityTypeConfiguration<LockerMerchant>
    {
        public void Configure(EntityTypeBuilder<LockerMerchant> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.LockerMerchants, WashGoDbProperties.Schema);
            builder.HasIndex(x => new { x.LockerId, x.MerchantId }).IsUnique();
            builder.HasIndex(x => x.MerchantId);
            builder.HasOne<Locker>().WithMany().HasForeignKey(x => x.LockerId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
