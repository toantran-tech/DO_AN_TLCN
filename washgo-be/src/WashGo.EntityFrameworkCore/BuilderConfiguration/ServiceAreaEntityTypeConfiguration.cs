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

    public class ServiceAreaEntityTypeConfiguration : IEntityTypeConfiguration<ServiceArea>
    {
        public void Configure(EntityTypeBuilder<ServiceArea> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ServiceAreas, WashGoDbProperties.Schema);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.District).HasMaxLength(100).IsRequired();
            builder.Property(x => x.City).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Latitude).HasPrecision(10, 8);
            builder.Property(x => x.Longitude).HasPrecision(11, 8);
            builder.Property(x => x.RadiusKm).HasPrecision(5, 2);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.HasIndex(x => x.District);
            builder.HasIndex(x => x.IsActive);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
