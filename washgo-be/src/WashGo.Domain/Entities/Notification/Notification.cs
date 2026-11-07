using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Notification
{
    [SevagoTable(WashGoDbProperties.Notifications, Schema = WashGoDbProperties.Schema)]
    public class Notification : BaseEntity
    {
        public Guid UserId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        /// <summary>Loại đối tượng liên kết để client điều hướng: "order", "incident", "rating"; null nếu không có.</summary>
        public string? LinkType { get; set; }
        public Guid? LinkId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        /// <summary>Khóa mẫu câu để client tự dịch (vd. "orderCreated.customer"); null với thông báo cũ.</summary>
        public string? MessageKey { get; set; }
        /// <summary>Tham số của mẫu câu, JSON object chuỗi → chuỗi (mã đơn, locker, số tiền…).</summary>
        public string? MessageArgs { get; set; }
    }
}
