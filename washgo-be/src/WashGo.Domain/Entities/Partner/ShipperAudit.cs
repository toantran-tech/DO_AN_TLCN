using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{
    /// <summary>Vết thao tác khóa/mở khóa/xóa/sửa giấy tờ Shipper: ai làm, lúc nào, lý do.</summary>
    [SevagoTable(WashGoDbProperties.ShipperAudits, Schema = WashGoDbProperties.Schema)]
    public class ShipperAudit : Entity<Guid>
    {
        public ShipperAudit() { }
        public ShipperAudit(Guid id) : base(id) { }

        public Guid ShipperId { get; set; }
        public Guid MerchantId { get; set; }
        public ShipperAuditAction Action { get; set; }
        public string? Reason { get; set; }
        public Guid ActorId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        /// <summary>Thông tin thêm (vd. trường giấy tờ đã đổi); không chứa số/ảnh giấy tờ.</summary>
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
