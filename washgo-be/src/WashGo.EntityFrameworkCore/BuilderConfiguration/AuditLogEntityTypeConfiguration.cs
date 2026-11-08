using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class AuditLogEntityTypeConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.AuditLogs, WashGoDbProperties.Schema);
            builder.Property(x => x.ActorName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.ActorRole).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Module).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
            builder.Property(x => x.ResourceType).HasMaxLength(40).IsRequired();
            builder.Property(x => x.ResourceId).HasMaxLength(64);
            builder.Property(x => x.ResourceCode).HasMaxLength(200);
            builder.Property(x => x.Reason).HasMaxLength(1000);
            builder.Property(x => x.ErrorCode).HasMaxLength(60);
            builder.Property(x => x.ChangedFields).HasMaxLength(1000);
            builder.Property(x => x.Details).HasColumnType("jsonb");
            builder.Property(x => x.CorrelationId).HasMaxLength(100);
            // No foreign keys: the trail must outlive (soft-)deleted users and resources.
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => new { x.ActorId, x.OccurredAt });
            builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
        }
    }
}
