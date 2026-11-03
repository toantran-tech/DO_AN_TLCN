using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Locker
{
    /// <summary>Merchant phụ trách locker (một locker có thể có nhiều Merchant); Admin chọn một trong số này cho từng đơn (UC 3.3).</summary>
    [SevagoTable(WashGoDbProperties.LockerMerchants, Schema = WashGoDbProperties.Schema)]
    public class LockerMerchant : Entity<Guid>
    {
        public LockerMerchant() { }
        public LockerMerchant(Guid id) : base(id) { }

        public Guid LockerId { get; set; }
        public Guid MerchantId { get; set; }
        /// <summary>1 là ưu tiên cao nhất; dùng để gợi ý Merchant khi phân công.</summary>
        public int Priority { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
