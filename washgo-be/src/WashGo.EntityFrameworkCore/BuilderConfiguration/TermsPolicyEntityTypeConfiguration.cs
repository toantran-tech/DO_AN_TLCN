using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Policy;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class TermsPolicyEntityTypeConfiguration : IEntityTypeConfiguration<TermsPolicy>
    {
        public void Configure(EntityTypeBuilder<TermsPolicy> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.TermsPolicies, WashGoDbProperties.Schema);
            builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Content).IsRequired();
            builder.HasIndex(x => x.Version).IsUnique();
            builder.HasIndex(x => x.EffectiveFrom);
        }
    }
}
