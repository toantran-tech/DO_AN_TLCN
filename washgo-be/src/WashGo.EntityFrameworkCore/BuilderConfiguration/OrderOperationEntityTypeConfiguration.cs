using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class OrderOperationEntityTypeConfiguration : IEntityTypeConfiguration<OrderOperation>
    {
        public void Configure(EntityTypeBuilder<OrderOperation> builder)
        {
            builder.ToTable(WashGoDbProperties.OrderOperations, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            // The key is the client's operation id, never generated.
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Operation).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Request).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => x.OrderId);
            builder.HasOne<WashOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
