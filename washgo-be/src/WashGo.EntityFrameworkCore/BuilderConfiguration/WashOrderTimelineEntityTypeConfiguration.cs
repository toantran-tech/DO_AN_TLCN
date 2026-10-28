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

    public class WashOrderTimelineEntityTypeConfiguration : IEntityTypeConfiguration<WashOrderTimeline>
    {
        public void Configure(EntityTypeBuilder<WashOrderTimeline> builder)
        {
            builder.ToTable(WashGoDbProperties.OrderStatusHistory, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property(x => x.WashOrderId).HasColumnName("order_id");
            builder.Property(x => x.FromStatus).HasColumnName("from_status");
            builder.Property(x => x.Status).HasColumnName("to_status");
            builder.Property(x => x.ActorId).HasColumnName("changed_by");
            builder.Property(x => x.ActorName).HasMaxLength(150);
            builder.Property(x => x.ActorRole).HasColumnName("changed_by_type").HasMaxLength(50);
            builder.Property(x => x.Timestamp).HasColumnName("created_at");
            builder.Property(x => x.Note).HasMaxLength(500);
        }
    }
}
