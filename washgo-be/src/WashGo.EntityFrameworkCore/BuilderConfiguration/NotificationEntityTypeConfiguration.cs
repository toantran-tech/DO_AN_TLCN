using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class NotificationEntityTypeConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable(WashGoDbProperties.Notifications, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Content).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.LinkType).HasMaxLength(30);
            builder.HasIndex(x => new { x.UserId, x.IsDeleted, x.IsRead });
            builder.HasIndex(x => new { x.UserId, x.CreatedOn });
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
