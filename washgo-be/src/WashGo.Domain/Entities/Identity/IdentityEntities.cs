using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{
    [SevagoTable(WashGoDbProperties.Roles, Schema = WashGoDbProperties.Schema)]
    public class Role : Entity<Guid>
    {
        public Role() { }
        public Role(Guid id) : base(id) { }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.Users, Schema = WashGoDbProperties.Schema)]
    public class User : Entity<Guid>
    {
        public User() { }
        public User(Guid id) : base(id) { }

        public Guid RoleId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;
        public string? ProviderId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.RefreshTokens, Schema = WashGoDbProperties.Schema)]
    public class RefreshToken : Entity<Guid>
    {
        public RefreshToken() { }
        public RefreshToken(Guid id) : base(id) { }

        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.OtpCodes, Schema = WashGoDbProperties.Schema)]
    public class OtpCode : Entity<Guid>
    {
        public OtpCode() { }
        public OtpCode(Guid id) : base(id) { }

        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; } = 5;
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}