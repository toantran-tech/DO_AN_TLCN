using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{

    /// <summary>
    /// Tài khoản đăng nhập. Không bao giờ xóa cứng: xóa = xóa mềm (IsDeleted). Không dùng bộ lọc ISoftDelete tự động của ABP
    /// vì đơn/biên nhận/đánh giá cũ vẫn phải đọc được người đã xóa; mọi luồng định danh (đăng nhập, đăng ký, Google, token,
    /// danh sách quản trị) lọc IsDeleted rõ ràng. Email/provider chỉ unique trong tập tài khoản chưa xóa.
    /// </summary>
    [SevagoTable(WashGoDbProperties.Users, Schema = WashGoDbProperties.Schema)]
    public class User : Entity<Guid>
    {
        public User() { }
        public User(Guid id) : base(id) { }

        public Guid RoleId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;
        public string? ProviderId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        /// <summary>Lần đổi/đặt lại mật khẩu gần nhất (null = chưa đổi kể từ khi có trường này).</summary>
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Lần khóa/mở gần nhất (giờ server), người thực hiện và lý do; lịch sử đầy đủ ở audit.</summary>
        public DateTime? StatusChangedAt { get; set; }
        public Guid? StatusChangedBy { get; set; }
        public string? StatusReason { get; set; }

        public bool IsDeleted { get; set; }
        /// <summary>Giờ server ghi nhận xóa thành công (UTC); null khi chưa xóa.</summary>
        public DateTime? DeletedAt { get; set; }
        /// <summary>Người xóa; null với xóa do hệ thống/di trú.</summary>
        public Guid? DeletedBy { get; set; }
        public string? DeleteReason { get; set; }

        /// <summary>Tài khoản hệ thống/khôi phục khẩn cấp: không thao tác qua trang quản lý tài khoản (kể cả hàng loạt).</summary>
        public bool IsProtected { get; set; }

        /// <summary>Kết quả gửi mật khẩu tạm cho tài khoản Customer/Admin tạo từ trang quản lý tài khoản; null = không áp dụng.</summary>
        public CredentialDeliveryStatus? CredentialDelivery { get; set; }
        public DateTime? CredentialSentAt { get; set; }

        /// <summary>DB tự tăng khi đổi dữ liệu quản trị (không tăng khi chỉ ghi lần đăng nhập); dùng cho expectedRevision.</summary>
        public long Revision { get; set; }
    }
}
