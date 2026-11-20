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
        /// <summary>Unique lower(email) chỉ trên tài khoản chưa xóa (tạo bằng SQL trong migration AdminAccountManagement).</summary>
        public const string ActiveEmailIndex = "ux_users_email_active";
        /// <summary>Unique (auth_provider, provider_id) chỉ trên tài khoản chưa xóa có provider_id.</summary>
        public const string ActiveProviderIndex = "ux_users_provider_active";

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
            builder.Property(x => x.StatusReason).HasMaxLength(500);
            builder.Property(x => x.DeleteReason).HasMaxLength(500);
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.Property(x => x.IsProtected).HasDefaultValue(false);
            // Bumped by trigger trg_users_revision on administrative changes; EF reads the new value back.
            builder.Property(x => x.Revision).HasDefaultValue(0L).ValueGeneratedOnAddOrUpdate();
            builder.HasIndex(x => x.RoleId);
            builder.HasIndex(x => new { x.CreatedAt, x.Id }).HasFilter("is_deleted = false").HasDatabaseName("ix_users_created_active");
            builder.HasIndex(x => x.DeletedAt).HasFilter("is_deleted = true").HasDatabaseName("ix_users_deleted_at");
            builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "chk_users_auth",
                    "(auth_provider = 1 AND password_hash IS NOT NULL) OR (auth_provider <> 1 AND provider_id IS NOT NULL)");
                // Deleted ⇔ deleted_at set; a deleted account can never be active.
                t.HasCheckConstraint("chk_users_deleted",
                    "(is_deleted AND deleted_at IS NOT NULL AND NOT is_active) OR (NOT is_deleted AND deleted_at IS NULL AND deleted_by IS NULL AND delete_reason IS NULL)");
            });
        }
    }

    public class UserPermissionEntityTypeConfiguration : IEntityTypeConfiguration<UserPermission>
    {
        public void Configure(EntityTypeBuilder<UserPermission> builder)
        {
            builder.ToTable(WashGoDbProperties.UserPermissions, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Permission).HasMaxLength(60).IsRequired();
            builder.HasIndex(x => new { x.UserId, x.Permission }).IsUnique();
            builder.HasIndex(x => x.Permission);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
