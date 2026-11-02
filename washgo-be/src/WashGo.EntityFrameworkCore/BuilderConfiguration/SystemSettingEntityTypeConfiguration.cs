using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class SystemSettingEntityTypeConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.ToTable(WashGoDbProperties.SystemSettings, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.DefaultCommissionRate).HasPrecision(5, 2);
            builder.Property(x => x.OverdueFeeDay1).HasPrecision(18, 2);
            builder.Property(x => x.OverdueFeeDay2).HasPrecision(18, 2);
            builder.Property(x => x.OverdueFeeDay3).HasPrecision(18, 2);
            builder.Property(x => x.StorageFeePerDay).HasPrecision(18, 2);
            // The single settings row ships with the migration so every environment starts from the same rules.
            builder.HasData(new SystemSetting());
        }
    }
}
