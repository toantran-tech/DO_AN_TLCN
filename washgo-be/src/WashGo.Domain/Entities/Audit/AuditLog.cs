using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Audit
{
    /// <summary>
    /// Nhật ký hoạt động, chỉ ghi thêm (không có API sửa/xóa): ai, lúc nào, trên đối tượng nào, đổi gì, kết quả.
    /// Không lưu mật khẩu, OTP, token, mã mở tủ, số/ảnh giấy tờ; chỉ lưu tên trường đã đổi và chi tiết đã lọc.
    /// </summary>
    [SevagoTable(WashGoDbProperties.AuditLogs, Schema = WashGoDbProperties.Schema)]
    public class AuditLog : Entity<Guid>
    {
        public AuditLog() { }
        public AuditLog(Guid id) : base(id) { }

        public DateTime OccurredAt { get; set; }
        public Guid? ActorId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        /// <summary>Admin, Merchant, Shipper, Customer hoặc System.</summary>
        public string ActorRole { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        /// <summary>Mã dễ nhận diện: mã đơn, email, tên cửa hàng…</summary>
        public string? ResourceCode { get; set; }
        public AuditResult Result { get; set; } = AuditResult.Success;
        public string? Reason { get; set; }
        public string? ErrorCode { get; set; }
        /// <summary>Tên các trường đã đổi, cách nhau bởi dấu phẩy.</summary>
        public string? ChangedFields { get; set; }
        /// <summary>JSON chi tiết đã lọc dữ liệu nhạy cảm.</summary>
        public string? Details { get; set; }
        public string? CorrelationId { get; set; }
    }
}
