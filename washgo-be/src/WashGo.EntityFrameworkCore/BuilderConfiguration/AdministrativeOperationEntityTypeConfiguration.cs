using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Audit;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class AdministrativeOperationEntityTypeConfiguration : IEntityTypeConfiguration<AdministrativeOperation>
{
    public void Configure(EntityTypeBuilder<AdministrativeOperation> b)
    {
        b.ToTable("administrative_operations", "washgo");
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Module).HasMaxLength(40); b.Property(x => x.Action).HasMaxLength(60);
        b.Property(x => x.ResourceId).HasMaxLength(80); b.Property(x => x.PayloadHash).HasMaxLength(64);
        b.Property(x => x.ResponseJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ActorId, x.Module, x.CompletedAt });
    }
}
