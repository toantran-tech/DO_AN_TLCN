using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class PrivateDocumentEntityTypeConfiguration : IEntityTypeConfiguration<PrivateDocument>
{
    public void Configure(EntityTypeBuilder<PrivateDocument> b)
    {
        b.ToTable("private_documents", WashGoDbProperties.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FileName).HasMaxLength(180);
        b.Property(x => x.Extension).HasMaxLength(8);
        b.Property(x => x.ContentType).HasMaxLength(120);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => new { x.IsDeleted, x.ReferencedAt, x.ExpiresAt });
        b.HasIndex(x => x.MerchantId);
        b.HasIndex(x => x.OwnerId);
    }
}
