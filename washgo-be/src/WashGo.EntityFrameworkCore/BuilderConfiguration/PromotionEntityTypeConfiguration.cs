using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class PromotionEntityTypeConfiguration : IEntityTypeConfiguration<Promotion>
    {
        public void Configure(EntityTypeBuilder<Promotion> builder)
        {
            builder.ToTable(WashGoDbProperties.Promotions, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
            builder.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
            builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
            // Soft-deleted codes may be reused by a new promotion.
            builder.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
            builder.HasIndex(x => new { x.StartAt, x.EndAt });
            builder.HasIndex(x => x.IsActive);
        }
    }
}
