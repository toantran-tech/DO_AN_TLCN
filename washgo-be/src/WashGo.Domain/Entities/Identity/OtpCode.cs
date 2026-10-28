using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{

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
