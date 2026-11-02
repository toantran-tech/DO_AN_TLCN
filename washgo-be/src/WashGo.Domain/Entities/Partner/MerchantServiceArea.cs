using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

    [SevagoTable(WashGoDbProperties.MerchantServiceAreas, Schema = WashGoDbProperties.Schema)]
    public class MerchantServiceArea : Entity<Guid>
    {
        public MerchantServiceArea() { }
        public MerchantServiceArea(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }
        public Guid ServiceAreaId { get; set; }
        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public Guid? AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
