using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Results
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? LinkType { get; set; }
        public Guid? LinkId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public long CreatedOn { get; set; }
        /// <summary>Nhóm hiển thị: order, payment, promotion, feedback, system.</summary>
        public string Group { get; set; } = string.Empty;
        /// <summary>Khóa mẫu câu để FE dịch theo ngôn ngữ; title/content vẫn là bản tiếng Việt do BE tạo.</summary>
        public string MessageKey { get; set; } = string.Empty;
        public Dictionary<string, string> MessageArgs { get; set; } = [];
    }

    public class NotificationGroupCountDto
    {
        public string Group { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Unread { get; set; }
    }

    /// <summary>Kết quả lọc hộp thông báo kèm số lượng theo nhóm (toàn bộ hộp thư, không phụ thuộc bộ lọc).</summary>
    public class NotificationPageDto
    {
        public List<NotificationDto> Items { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int Take { get; set; }
        public int UnreadTotal { get; set; }
        public List<NotificationGroupCountDto> Counts { get; set; } = [];
    }

    /// <summary>Sự kiện SignalR "notificationState": thiết bị khác của cùng tài khoản cập nhật ngay khi đọc/xóa.</summary>
    public class NotificationStateDto
    {
        /// <summary>read, readAll, deleted, deletedAll.</summary>
        public string Action { get; set; } = string.Empty;
        public List<Guid> Ids { get; set; } = [];
        public int UnreadCount { get; set; }
        public DateTime At { get; set; }
    }
}
