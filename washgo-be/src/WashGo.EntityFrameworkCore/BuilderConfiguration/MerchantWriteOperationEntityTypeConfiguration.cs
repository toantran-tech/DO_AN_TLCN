using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class MerchantWriteOperationEntityTypeConfiguration : IEntityTypeConfiguration<MerchantWriteOperation>
{
    public void Configure(EntityTypeBuilder<MerchantWriteOperation> builder)
    {
        builder.ToTable("merchant_write_operations", WashGoDbProperties.Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Operation).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.ActorId, x.MerchantId });
    }
}
