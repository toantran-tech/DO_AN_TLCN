using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Shared.Constants;
using WashOrderEntity = WashGo.Domain.Entities.WashOrder.WashOrder;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class IncidentEntityTypeConfiguration : IEntityTypeConfiguration<Incident>
    {
        public void Configure(EntityTypeBuilder<Incident> builder)
        {
            builder.ToTable(WashGoDbProperties.Incidents, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.OrderCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.MerchantName).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            builder.Property(x => x.ResolutionNote).HasMaxLength(2000);
            builder.Property(x => x.ItemValue).HasPrecision(18, 2);
            builder.Property(x => x.DeclaredItemValue).HasPrecision(18, 2);
            builder.Property(x => x.CompensationAmount).HasPrecision(18, 2);
            builder.Property(x => x.CompensationReference).HasMaxLength(120);
            builder.Property(x => x.ClientRequestId).HasMaxLength(64);
            builder.Property(x => x.WithdrawReason).HasMaxLength(500);
            builder.HasIndex(x => new { x.CustomerId, x.ClientRequestId }).IsUnique().HasFilter("client_request_id IS NOT NULL");
            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.CustomerId);
            builder.HasIndex(x => new { x.Status, x.Severity });
            builder.HasIndex(x => x.MerchantId);
            builder.HasOne<WashOrderEntity>()
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
