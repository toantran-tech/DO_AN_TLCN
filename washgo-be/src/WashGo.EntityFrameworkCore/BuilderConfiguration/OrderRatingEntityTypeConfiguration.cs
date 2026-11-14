using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Shared.Constants;
using WashOrderEntity = WashGo.Domain.Entities.WashOrder.WashOrder;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class OrderRatingEntityTypeConfiguration : IEntityTypeConfiguration<OrderRating>
    {
        public void Configure(EntityTypeBuilder<OrderRating> builder)
        {
            builder.ToTable(WashGoDbProperties.OrderRatings, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.OrderCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.MerchantName).HasMaxLength(200);
            builder.Property(x => x.Comment).HasMaxLength(1000);
            builder.Property(x => x.HiddenReason).HasMaxLength(500);
            // One rating per order; also guards against concurrent double submits.
            builder.HasIndex(x => x.OrderId).IsUnique();
            builder.HasIndex(x => new { x.MerchantId, x.IsHidden });
            builder.HasIndex(x => x.OverallStars);
            builder.HasIndex(x => x.PublicId).IsUnique();
            builder.HasIndex(x => new { x.PublicApprovedAt, x.PublicId });
            builder.HasOne<WashOrderEntity>()
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
