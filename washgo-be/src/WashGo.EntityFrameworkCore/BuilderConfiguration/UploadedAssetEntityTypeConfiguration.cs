using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Partner;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class UploadedAssetEntityTypeConfiguration : IEntityTypeConfiguration<UploadedAsset>
{
    public void Configure(EntityTypeBuilder<UploadedAsset> b)
    {
        b.ToTable("uploaded_assets", "washgo"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Url).HasMaxLength(180); b.HasIndex(x => x.Url).IsUnique();
        b.HasIndex(x => new { x.IsDeleted, x.ReferencedAt, x.ExpiresAt });
    }
}
