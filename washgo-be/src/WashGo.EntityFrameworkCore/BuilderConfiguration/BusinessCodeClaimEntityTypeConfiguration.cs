using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class BusinessCodeClaimEntityTypeConfiguration : IEntityTypeConfiguration<BusinessCodeClaim>
{
    public void Configure(EntityTypeBuilder<BusinessCodeClaim> b)
    {
        b.ToTable("business_code_claims", "washgo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("code").HasMaxLength(6).ValueGeneratedNever();
        b.Property(x => x.Prefix).HasMaxLength(2).IsRequired();
        b.HasIndex(x => x.ResourceId).IsUnique();
        b.HasIndex(x => x.Prefix);
    }
}
