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

    public class MerchantImageEntityTypeConfiguration : IEntityTypeConfiguration<MerchantImage>
    {
        public void Configure(EntityTypeBuilder<MerchantImage> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.MerchantImages, WashGoDbProperties.Schema);
            builder.Property(x => x.ImageType).HasMaxLength(50).IsRequired();
            builder.Property(x => x.PublicId).HasMaxLength(255).IsRequired();
            builder.Property(x => x.SecureUrl).HasMaxLength(500).IsRequired();
            builder.Property(x => x.Metadata).HasColumnType("jsonb");
            builder.HasIndex(x => x.MerchantId);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
