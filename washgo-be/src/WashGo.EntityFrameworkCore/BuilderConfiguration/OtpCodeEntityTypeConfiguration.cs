using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class OtpCodeEntityTypeConfiguration : IEntityTypeConfiguration<OtpCode>
    {
        public void Configure(EntityTypeBuilder<OtpCode> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.OtpCodes, WashGoDbProperties.Schema);
            builder.Property(x => x.Email).HasMaxLength(100).IsRequired();
            builder.Property(x => x.CodeHash).HasColumnName("otp_code").HasMaxLength(255).IsRequired();
            builder.Property(x => x.IpAddress).HasMaxLength(50);
            builder.HasIndex(x => x.Email);
            builder.HasIndex(x => x.Purpose);
            builder.HasIndex(x => x.ExpiryDate);
            builder.HasIndex(x => new { x.Email, x.Purpose, x.IsUsed });
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
