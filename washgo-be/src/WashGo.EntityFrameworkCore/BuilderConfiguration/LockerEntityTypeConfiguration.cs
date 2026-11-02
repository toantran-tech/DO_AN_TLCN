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
}
