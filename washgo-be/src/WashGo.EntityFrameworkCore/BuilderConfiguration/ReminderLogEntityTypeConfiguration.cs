using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class ReminderLogEntityTypeConfiguration : IEntityTypeConfiguration<ReminderLog>
    {
        public void Configure(EntityTypeBuilder<ReminderLog> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ReminderLogs, WashGoDbProperties.Schema);
            builder.Property(x => x.Key).HasMaxLength(150).IsRequired();
            builder.HasIndex(x => x.Key).IsUnique();
            builder.HasIndex(x => x.OrderId);
        }
    }
}
