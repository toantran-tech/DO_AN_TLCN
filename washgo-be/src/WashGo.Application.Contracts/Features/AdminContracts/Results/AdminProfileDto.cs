using System;

namespace WashGo.Application.Contracts.Features.AdminContracts.Results
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
        /// <summary>Phiên bản hồ sơ dạng chuỗi opaque (không parse thành số): gửi lại nguyên văn trong expectedVersion; lệch → 409.</summary>
        public string Version { get; set; } = string.Empty;
        /// <summary>Các trường được phép tự sửa. Email không sửa tại đây (cần quy trình xác minh riêng).</summary>
        public string[] EditableFields { get; set; } = ["fullName", "phone", "avatar"];
    }
}
