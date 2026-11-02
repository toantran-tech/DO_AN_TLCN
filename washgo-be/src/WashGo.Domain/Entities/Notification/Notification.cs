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
    }
}
