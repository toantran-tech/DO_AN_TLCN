using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class ShipperAuditEntityTypeConfiguration : IEntityTypeConfiguration<ShipperAudit>
    {
        public void Configure(EntityTypeBuilder<ShipperAudit> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ShipperAudits, WashGoDbProperties.Schema);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.ActorRole).HasMaxLength(20).IsRequired();
            builder.Property(x => x.ActorName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Details).HasMaxLength(1000);
            builder.HasIndex(x => new { x.ShipperId, x.CreatedAt });
            // Audit rows outlive a (soft-)deleted shipper; no cascade from the shipper row.
            builder.HasIndex(x => x.MerchantId);
        }
    }
}
