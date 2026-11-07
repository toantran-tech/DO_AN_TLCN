using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    public class ShipperAuditDto
    {
        public Guid Id { get; set; }
        /// <summary>1 Locked, 2 Unlocked, 3 Deleted, 4 IdentityUpdated.</summary>
        public ShipperAuditAction Action { get; set; }
        public string? Reason { get; set; }
        public Guid ActorId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
