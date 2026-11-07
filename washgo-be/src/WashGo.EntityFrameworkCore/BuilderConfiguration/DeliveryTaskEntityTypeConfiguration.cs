using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class DeliveryTaskEntityTypeConfiguration : IEntityTypeConfiguration<DeliveryTask>
    {
        public void Configure(EntityTypeBuilder<DeliveryTask> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.DeliveryTasks, WashGoDbProperties.Schema);
            builder.HasIndex(x => new { x.ShipperId, x.Status });
            builder.HasIndex(x => x.OrderId);
            builder.HasOne<WashOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
