using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Queries
{
    /// <summary>Lọc hộp thông báo của tài khoản đang đăng nhập.</summary>
    public class NotificationFilterQuery
    {
        /// <summary>Tìm trong tiêu đề/nội dung (không phân biệt hoa thường).</summary>
        public string? Keyword { get; set; }
        /// <summary>Nhóm: order, payment, promotion, feedback, system (ghép OR).</summary>
        public List<string>? Groups { get; set; }
        /// <summary>Loại thông báo cụ thể (ghép OR, AND với nhóm).</summary>
        public List<NotificationType>? Types { get; set; }
        public bool? UnreadOnly { get; set; }
        /// <summary>Unix giây (UTC), tính theo thời điểm tạo thông báo.</summary>
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        /// <summary>newest (mặc định), oldest, unreadFirst.</summary>
        public string? Sort { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
