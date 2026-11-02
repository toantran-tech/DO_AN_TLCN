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

    public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.Users, WashGoDbProperties.Schema);
            builder.Property(x => x.Email).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PasswordHash).HasMaxLength(255);
            builder.Property(x => x.ProviderId).HasMaxLength(255);
            builder.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Phone).HasMaxLength(20);
            builder.Property(x => x.Avatar)
                .HasColumnType("jsonb")
                .HasConversion(
                    url => JsonSerializer.Serialize(url, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<string>(json, (JsonSerializerOptions?)null));
            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.RoleId);
            builder.HasIndex(x => new { x.AuthProvider, x.ProviderId }).IsUnique();
            builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            builder.ToTable(t => t.HasCheckConstraint(
                "chk_users_auth",
                "(auth_provider = 1 AND password_hash IS NOT NULL) OR (auth_provider <> 1 AND provider_id IS NOT NULL)"));
        }
    }
}
