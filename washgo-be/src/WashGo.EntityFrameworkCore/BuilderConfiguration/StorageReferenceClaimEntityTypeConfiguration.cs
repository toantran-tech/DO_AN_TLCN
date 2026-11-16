using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashOrder;
namespace WashGo.EntityFrameworkCore.BuilderConfiguration;
public class StorageReferenceClaimEntityTypeConfiguration : IEntityTypeConfiguration<StorageReferenceClaim>
{
    public void Configure(EntityTypeBuilder<StorageReferenceClaim> b)
    {
        b.ToTable("storage_reference_claims", "washgo"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}
