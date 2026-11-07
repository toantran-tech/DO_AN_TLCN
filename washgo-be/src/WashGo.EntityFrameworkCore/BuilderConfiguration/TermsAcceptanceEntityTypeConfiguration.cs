using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Policy;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class TermsAcceptanceEntityTypeConfiguration : IEntityTypeConfiguration<TermsAcceptance>
    {
        public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.TermsAcceptances, WashGoDbProperties.Schema);
            builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
            builder.Property(x => x.IpAddress).HasMaxLength(64);
            builder.Property(x => x.UserAgent).HasMaxLength(500);
            // One acceptance per account and version: replays are answered from the stored row.
            builder.HasIndex(x => new { x.UserId, x.PolicyId }).IsUnique();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<TermsPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
