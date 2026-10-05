using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class LockerEntityTypeConfiguration : EntityBaseConfiguration<Locker>
    {
        public override void Configure(EntityTypeBuilder<Locker> builder)
        {
            base.Configure(builder);
            builder.ToTable(WashGoDbProperties.Lockers, WashGoDbProperties.Schema);
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Address).HasColumnType("text").IsRequired();
            builder.Property(x => x.District).HasMaxLength(100);
            builder.Property(x => x.City).HasMaxLength(100);
            builder.Property(x => x.TotalBoxes).HasColumnName("total_slots").HasDefaultValue(10);
            builder.Property(x => x.AvailableBoxes).HasColumnName("available_slots").HasDefaultValue(10);
            builder.Property(x => x.Images).HasColumnType("jsonb");
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.ServiceAreaId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => new { x.Latitude, x.Longitude });
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ServiceArea>().WithMany().HasForeignKey(x => x.ServiceAreaId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
            builder.HasMany(x => x.Boxes).WithOne().HasForeignKey(x => x.LockerId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class LockerBoxEntityTypeConfiguration : EntityBaseConfiguration<LockerBox>
    {
        public override void Configure(EntityTypeBuilder<LockerBox> builder)
        {
            base.Configure(builder);
            builder.ToTable(WashGoDbProperties.LockerSlots, WashGoDbProperties.Schema);
            builder.Property(x => x.SlotNumber).HasColumnName("slot_number");
            builder.HasIndex(x => new { x.LockerId, x.SlotNumber }).IsUnique();
            builder.HasIndex(x => x.Status);
        }
    }

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