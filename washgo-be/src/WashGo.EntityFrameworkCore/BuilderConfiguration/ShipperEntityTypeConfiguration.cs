using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class ShipperEntityTypeConfiguration : IEntityTypeConfiguration<Shipper>
    {
        public void Configure(EntityTypeBuilder<Shipper> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.Shippers, WashGoDbProperties.Schema);
            builder.Property(x => x.VehiclePlate).HasMaxLength(20);
            builder.Property(x => x.IdentityNumber).HasMaxLength(20);
            builder.Property(x => x.IdentityImages).HasColumnType("jsonb");
            builder.Property(x => x.Rating).HasPrecision(2, 1);
            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.Status);
            builder.HasOne<User>().WithOne().HasForeignKey<Shipper>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
