using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class IncidentMessageEntityTypeConfiguration : IEntityTypeConfiguration<IncidentMessage>
    {
        public void Configure(EntityTypeBuilder<IncidentMessage> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.IncidentMessages, WashGoDbProperties.Schema);
            builder.Property(x => x.SenderRole).HasMaxLength(20).IsRequired();
            builder.Property(x => x.SenderName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            builder.HasIndex(x => new { x.IncidentId, x.CreatedAt });
            builder.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
