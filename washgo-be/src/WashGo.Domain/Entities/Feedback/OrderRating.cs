using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Feedback
{
    [SevagoTable(WashGoDbProperties.OrderRatings, Schema = WashGoDbProperties.Schema)]
    public class OrderRating : BaseEntity
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public int OverallStars { get; set; }
        public int? QualityStars { get; set; }
        public int? SpeedStars { get; set; }
        public int? AttitudeStars { get; set; }
        public int? PackagingStars { get; set; }
        public string? Comment { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
        public bool IsHidden { get; set; }
        public string? HiddenReason { get; set; }
        public Guid? HiddenBy { get; set; }
        public DateTime? HiddenAt { get; set; }
        /// <summary>Lần Admin đổi trạng thái ẩn/hiển thị gần nhất. Không dùng làm bằng chứng phê duyệt nội dung.</summary>
        public DateTime? ModeratedAt { get; set; }
        public Guid? ModeratedBy { get; set; }
        /// <summary>Content approval is independent of visibility moderation; hiding never approves content.</summary>
        public DateTime? ContentApprovedAt { get; set; }
        public Guid? ContentApprovedBy { get; set; }
    }
}
