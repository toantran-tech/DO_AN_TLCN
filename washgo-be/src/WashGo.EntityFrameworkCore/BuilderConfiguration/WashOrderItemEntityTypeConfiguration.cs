using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class WashOrderItemEntityTypeConfiguration : IEntityTypeConfiguration<WashOrderItem>
    {
        public void Configure(EntityTypeBuilder<WashOrderItem> builder)
        {
            builder.ToTable(WashGoDbProperties.OrderItems, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property(x => x.WashOrderId).HasColumnName("order_id");
            builder.Property(x => x.ServiceName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.WeightKg).HasPrecision(18, 2);
            builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
            builder.Property(x => x.SubTotal).HasColumnName("total_price").HasPrecision(18, 2);
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.HasOne<WashService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
