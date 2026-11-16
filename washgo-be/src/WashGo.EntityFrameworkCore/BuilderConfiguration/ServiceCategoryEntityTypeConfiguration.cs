using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashService;
namespace WashGo.EntityFrameworkCore.BuilderConfiguration;
public class ServiceCategoryEntityTypeConfiguration : IEntityTypeConfiguration<ServiceCategoryEntity>
{
    public void Configure(EntityTypeBuilder<ServiceCategoryEntity> b)
    {
        b.ToTable("service_categories", "washgo"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.NormalizedName).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}
