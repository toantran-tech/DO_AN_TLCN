using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class WashServiceEntityTypeConfiguration : EntityBaseConfiguration<WashService>
    {
        public override void Configure(EntityTypeBuilder<WashService> builder)
        {
            base.Configure(builder);
            builder.ToTable(WashGoDbProperties.Services, WashGoDbProperties.Schema);
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.UnitPrice).HasColumnName("base_price").HasPrecision(18, 2);
            builder.Property(x => x.PricePerKg).HasPrecision(18, 2);
            builder.Property(x => x.PricePerItem).HasPrecision(18, 2);
            builder.Property(x => x.ServiceType).HasColumnName("category");
            builder.Property(x => x.EstimatedDurationHours).HasColumnName("estimated_time");
            builder.Property(x => x.Unit).HasMaxLength(20);
            builder.Property(x => x.AllowedStatuses).HasColumnType("jsonb");
            builder.Property(x => x.Images).HasColumnType("jsonb");
            builder.Property(x => x.ApprovalStatus).HasColumnName("status");
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.ApprovalStatus);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
