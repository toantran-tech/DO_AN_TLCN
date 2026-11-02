using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{

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
}
