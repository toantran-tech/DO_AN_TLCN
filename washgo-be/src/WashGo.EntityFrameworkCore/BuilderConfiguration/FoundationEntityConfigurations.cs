using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    internal static class GuidKeyConfiguration
    {
        public static void ConfigureGeneratedGuid<TEntity>(EntityTypeBuilder<TEntity> builder)
            where TEntity : Volo.Abp.Domain.Entities.Entity<Guid>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
        }
    }

    public class RoleEntityTypeConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.Roles, WashGoDbProperties.Schema);
            builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Description).HasColumnType("text");
            builder.HasIndex(x => x.Name).IsUnique();
        }
    }

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
            builder.Property(x => x.Avatar).HasColumnType("jsonb");
            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.RoleId);
            builder.HasIndex(x => new { x.AuthProvider, x.ProviderId }).IsUnique();
            builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            builder.ToTable(t => t.HasCheckConstraint(
                "chk_users_auth",
                "(auth_provider = 1 AND password_hash IS NOT NULL) OR (auth_provider <> 1 AND provider_id IS NOT NULL)"));
        }
    }

    public class RefreshTokenEntityTypeConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.RefreshTokens, WashGoDbProperties.Schema);
            builder.Property(x => x.Token).HasMaxLength(500).IsRequired();
            builder.HasIndex(x => x.Token).IsUnique();
            builder.HasIndex(x => x.UserId);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }

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

    public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable(WashGoDbProperties.Customers, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("user_id").ValueGeneratedNever();
            builder.Property(x => x.District).HasMaxLength(100);
            builder.Property(x => x.City).HasMaxLength(100);
            builder.Property(x => x.TotalSpent).HasPrecision(18, 2);
            builder.HasOne<User>().WithOne().HasForeignKey<Customer>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServiceAreaEntityTypeConfiguration : IEntityTypeConfiguration<ServiceArea>
    {
        public void Configure(EntityTypeBuilder<ServiceArea> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ServiceAreas, WashGoDbProperties.Schema);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.District).HasMaxLength(100).IsRequired();
            builder.Property(x => x.City).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Latitude).HasPrecision(10, 8);
            builder.Property(x => x.Longitude).HasPrecision(11, 8);
            builder.Property(x => x.RadiusKm).HasPrecision(5, 2);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.HasIndex(x => x.District);
            builder.HasIndex(x => x.IsActive);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class MerchantEntityTypeConfiguration : IEntityTypeConfiguration<Merchant>
    {
        public void Configure(EntityTypeBuilder<Merchant> builder)
        {
            builder.ToTable(WashGoDbProperties.Merchants, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("user_id").ValueGeneratedNever();
            builder.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.TaxCode).HasMaxLength(50);
            builder.Property(x => x.BusinessLicense).HasColumnType("jsonb");
            builder.Property(x => x.District).HasMaxLength(100).IsRequired();
            builder.Property(x => x.City).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Latitude).HasPrecision(10, 8);
            builder.Property(x => x.Longitude).HasPrecision(11, 8);
            builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            builder.Property(x => x.OpeningHours).HasColumnType("jsonb");
            builder.Property(x => x.Rating).HasPrecision(2, 1);
            builder.Property(x => x.TotalRevenue).HasPrecision(18, 2);
            builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
            builder.Property(x => x.CommissionRate).HasPrecision(5, 2);
            builder.HasIndex(x => x.TaxCode).IsUnique();
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => new { x.Latitude, x.Longitude });
            builder.HasIndex(x => x.District);
            builder.HasIndex(x => x.City);
            builder.HasOne<User>().WithOne().HasForeignKey<Merchant>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class MerchantServiceAreaEntityTypeConfiguration : IEntityTypeConfiguration<MerchantServiceArea>
    {
        public void Configure(EntityTypeBuilder<MerchantServiceArea> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.MerchantServiceAreas, WashGoDbProperties.Schema);
            builder.HasIndex(x => new { x.MerchantId, x.ServiceAreaId }).IsUnique();
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ServiceArea>().WithMany().HasForeignKey(x => x.ServiceAreaId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class MerchantImageEntityTypeConfiguration : IEntityTypeConfiguration<MerchantImage>
    {
        public void Configure(EntityTypeBuilder<MerchantImage> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.MerchantImages, WashGoDbProperties.Schema);
            builder.Property(x => x.ImageType).HasMaxLength(50).IsRequired();
            builder.Property(x => x.PublicId).HasMaxLength(255).IsRequired();
            builder.Property(x => x.SecureUrl).HasMaxLength(500).IsRequired();
            builder.Property(x => x.Metadata).HasColumnType("jsonb");
            builder.HasIndex(x => x.MerchantId);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ShipperEntityTypeConfiguration : IEntityTypeConfiguration<Shipper>
    {
        public void Configure(EntityTypeBuilder<Shipper> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.Shippers, WashGoDbProperties.Schema);
            builder.Property(x => x.VehiclePlate).HasMaxLength(20);
            builder.Property(x => x.IdentityNumber).HasMaxLength(20);
            builder.Property(x => x.IdentityImages).HasColumnType("jsonb");
            builder.Property(x => x.Rating).HasPrecision(2, 1);
            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.Status);
            builder.HasOne<User>().WithOne().HasForeignKey<Shipper>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServiceChangeRequestEntityTypeConfiguration : IEntityTypeConfiguration<ServiceChangeRequest>
    {
        public void Configure(EntityTypeBuilder<ServiceChangeRequest> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ServiceChangeRequests, WashGoDbProperties.Schema);
            builder.Property(x => x.ProposedData).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.CurrentData).HasColumnType("jsonb");
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.ServiceId);
            builder.HasIndex(x => x.Status);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<WashService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}