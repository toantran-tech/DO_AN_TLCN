using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class LockerBoxEntityTypeConfiguration : EntityBaseConfiguration<LockerBox>
    {
        public override void Configure(EntityTypeBuilder<LockerBox> builder)
        {
            base.Configure(builder);
            builder.ToTable(WashGoDbProperties.LockerSlots, WashGoDbProperties.Schema);
            builder.Property(x => x.SlotNumber).HasColumnName("slot_number");
            builder.HasIndex(x => new { x.LockerId, x.SlotNumber }).IsUnique();
            builder.HasIndex(x => x.Status);
        }
    }
}
