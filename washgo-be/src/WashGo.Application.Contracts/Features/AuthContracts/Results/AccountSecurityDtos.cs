using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.AuthContracts.Results
{
    /// <summary>Một phiên đăng nhập còn hiệu lực (mỗi lần đăng nhập là một phiên; làm mới token giữ nguyên phiên về mặt người dùng).</summary>
    public class SessionDto
    {
        /// <summary>Id phiên logic, ổn định qua các lần làm mới token; dùng cho DELETE /auth/sessions/{id}.</summary>
        public Guid Id { get; set; }
        /// <summary>Lúc đăng nhập mở phiên (UTC).</summary>
        public DateTime StartedAt { get; set; }
        /// <summary>Lần làm mới gần nhất (UTC).</summary>
        public DateTime LastActiveAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? UserAgent { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class SecurityStatusDto
    {
        /// <summary>false với tài khoản chỉ đăng nhập Google (chưa có mật khẩu).</summary>
        public bool HasPassword { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool GoogleLinked { get; set; }
        /// <summary>Hệ thống chưa hỗ trợ xác thực hai lớp: luôn false, FE không hiển thị nút bật 2FA.</summary>
        public bool TwoFactorSupported { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public int ActiveSessions { get; set; }
    }

    /// <summary>Trang phiên đăng nhập; current luôn có (kể cả khi không nằm trong trang).</summary>
    public class SessionListDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int Take { get; set; }
        public bool HasMore { get; set; }
        public SessionDto? Current { get; set; }
        public List<SessionDto> Items { get; set; } = [];
    }

    public class RevokeSessionsResultDto
    {
        public int Revoked { get; set; }
        /// <summary>true khi phiên chọn đã kết thúc hoặc không còn token hiệu lực (danh sách đang xem đã cũ).</summary>
        public bool Stale { get; set; }
    }
}
