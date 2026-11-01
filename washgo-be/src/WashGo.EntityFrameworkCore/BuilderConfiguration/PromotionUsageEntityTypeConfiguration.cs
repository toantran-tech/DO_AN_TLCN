using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Shared.Constants;
using WashOrderEntity = WashGo.Domain.Entities.WashOrder.WashOrder;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class PromotionUsageEntityTypeConfiguration : IEntityTypeConfiguration<PromotionUsage>
    {
        public void Configure(EntityTypeBuilder<PromotionUsage> builder)
        {
            builder.ToTable(WashGoDbProperties.PromotionUsages, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.OrderAmount).HasPrecision(18, 2);
            builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            builder.HasIndex(x => new { x.PromotionId, x.CustomerId });
            builder.HasIndex(x => x.OrderId).IsUnique();
            builder.HasOne(x => x.Promotion)
                .WithMany()
                .HasForeignKey(x => x.PromotionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WashOrderEntity>()
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
