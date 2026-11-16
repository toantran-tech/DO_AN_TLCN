using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Notification;
namespace WashGo.EntityFrameworkCore.BuilderConfiguration;
public class NotificationDeliveryEntityTypeConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> b)
    {
        b.ToTable("notification_deliveries", "washgo"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.State).HasMaxLength(24);
        b.Property(x => x.LastError).HasMaxLength(100);
        b.HasIndex(x => x.NotificationId).IsUnique();
        b.HasIndex(x => new { x.State, x.NextAttemptAt });
        b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}
