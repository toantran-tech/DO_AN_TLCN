using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Promotion
{
    [SevagoTable(WashGoDbProperties.PromotionUsages, Schema = WashGoDbProperties.Schema)]
    public class PromotionUsage : BaseEntity
    {
        public Guid PromotionId { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime UsedAt { get; set; }
        /// <summary>Lượt dùng được trả lại khi đơn bị hủy; không tính vào thống kê và giới hạn.</summary>
        public bool IsReverted { get; set; }
        public DateTime? RevertedAt { get; set; }
        public virtual Promotion? Promotion { get; set; }
    }
}
