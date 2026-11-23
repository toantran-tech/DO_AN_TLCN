using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Validation;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class ShipperEntityTypeConfiguration : IEntityTypeConfiguration<Shipper>
    {
        public void Configure(EntityTypeBuilder<Shipper> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.Shippers, WashGoDbProperties.Schema, table =>
                table.HasCheckConstraint("ck_shippers_max_active_tasks", $"max_active_tasks BETWEEN {ShipperTaskLimits.Minimum} AND {ShipperTaskLimits.Maximum}"));
            builder.Property(x => x.MaxActiveTasks).HasDefaultValue(ShipperTaskLimits.Default);
            builder.Property(x => x.CreateRequestHash).HasMaxLength(64);
            builder.Property(x => x.VehiclePlate).HasMaxLength(20);
            builder.Property(x => x.IdentityNumber).HasMaxLength(20);
            builder.Property(x => x.IdentityImages).HasColumnType("jsonb");
            builder.Property(x => x.IdentityFrontImageUrl).HasMaxLength(500);
            builder.Property(x => x.IdentityBackImageUrl).HasMaxLength(500);
            builder.Property(x => x.Rating).HasPrecision(2, 1);
            builder.Property(x => x.LockReason).HasMaxLength(500);
            builder.Property(x => x.Revision).HasDefaultValue(0L).ValueGeneratedOnAddOrUpdate();
            builder.HasIndex(x => new { x.MerchantId, x.CreateRequestId }).IsUnique().HasFilter("create_request_id IS NOT NULL");
            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.Status);
            builder.HasOne<User>().WithOne().HasForeignKey<Shipper>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
