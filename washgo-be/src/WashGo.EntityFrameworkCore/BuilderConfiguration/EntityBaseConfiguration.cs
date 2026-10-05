using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Core.Domain.Common;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class EntityBaseConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();

            builder.HasIndex(d => d.SearchVector).HasMethod("GIN");

            builder.Property(x => x.CreatedOn)
                   .HasColumnType("bigint")
                   .HasDefaultValueSql("EXTRACT(EPOCH FROM now())::bigint");

            builder.Property(x => x.ModifiedOn)
                   .HasColumnType("bigint")
                   .HasDefaultValueSql("EXTRACT(EPOCH FROM now())::bigint");

            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.Property(x => x.Created).HasColumnType("jsonb");
            builder.Property(x => x.Modified).HasColumnType("jsonb");
        }
    }
}
