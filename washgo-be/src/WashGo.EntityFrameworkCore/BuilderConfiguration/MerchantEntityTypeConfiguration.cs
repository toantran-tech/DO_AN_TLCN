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

    public class MerchantEntityTypeConfiguration : IEntityTypeConfiguration<Merchant>
    {
        public void Configure(EntityTypeBuilder<Merchant> builder)
        {
            builder.ToTable(WashGoDbProperties.Merchants, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("user_id").ValueGeneratedNever();
            builder.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TaxCode).HasMaxLength(50);
            builder.Property(x => x.BusinessLicense).HasColumnType("jsonb");
            builder.Property(x => x.District).HasMaxLength(100).IsRequired();
            builder.Property(x => x.City).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Latitude).HasPrecision(10, 8);
            builder.Property(x => x.Longitude).HasPrecision(11, 8);
            builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            builder.Property(x => x.OpeningHours).HasColumnType("jsonb");
            builder.Property(x => x.Rating).HasPrecision(2, 1);
            builder.Property(x => x.TotalRevenue).HasPrecision(18, 2);
            builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
            builder.Property(x => x.CommissionRate).HasPrecision(5, 2);
            builder.HasIndex(x => x.TaxCode).IsUnique();
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => new { x.Latitude, x.Longitude });
            builder.HasIndex(x => x.District);
            builder.HasIndex(x => x.City);
            builder.HasOne<User>().WithOne().HasForeignKey<Merchant>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
