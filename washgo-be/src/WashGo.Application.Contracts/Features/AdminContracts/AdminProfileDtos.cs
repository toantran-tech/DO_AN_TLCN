using System;

namespace WashGo.Application.Contracts.Features.AdminContracts
{
    public class AdminProfileDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public string Role { get; set; } = "Admin";
        /// <summary>Ngày tạo tài khoản (UTC).</summary>
        public DateTime JoinedAt { get; set; }
        /// <summary>Active hoặc Locked.</summary>
        public string AccountStatus { get; set; } = "Active";
        public DateTime? LastLoginAt { get; set; }
        /// <summary>Phiên bản hồ sơ: gửi lại trong expectedVersion khi lưu; lệch → 409.</summary>
        public long Version { get; set; }
        /// <summary>Các trường được phép tự sửa. Email không sửa tại đây (cần quy trình xác minh riêng).</summary>
        public string[] EditableFields { get; set; } = ["fullName", "phone", "avatar"];
    }

    /// <summary>
    /// Admin tự sửa hồ sơ (chỉ chính mình, theo JWT). null = giữ nguyên; avatar "" = xóa ảnh;
    /// avatar mới phải là ảnh chính Admin upload qua /files/images. Không nhận userId/role/status/email.
    /// </summary>
    public class UpdateAdminProfileCommand
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        /// <summary>Bắt buộc: version lấy từ GET /admin/profile.</summary>
        public long? ExpectedVersion { get; set; }
    }
}
