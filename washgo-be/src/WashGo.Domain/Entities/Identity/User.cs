using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{

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
}
