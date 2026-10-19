using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class RefundRequestEntityTypeConfiguration : IEntityTypeConfiguration<RefundRequest>
    {
        public void Configure(EntityTypeBuilder<RefundRequest> builder)
        {
            builder.ToTable(WashGoDbProperties.RefundRequests, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.ReviewNote).HasMaxLength(1000);
            builder.Property(x => x.RefundReference).HasMaxLength(120);
            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.CustomerId);
            builder.HasIndex(x => x.Status);
            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
