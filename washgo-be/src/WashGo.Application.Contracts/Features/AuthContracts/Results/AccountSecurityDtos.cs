using System;

namespace WashGo.Application.Contracts.Features.AuthContracts.Results
{
    /// <summary>Một phiên đăng nhập còn hiệu lực (mỗi lần đăng nhập là một phiên; làm mới token giữ nguyên phiên về mặt người dùng).</summary>
    public class SessionDto
    {
        /// <summary>Id phiên hiện hành (đổi sau mỗi lần làm mới token).</summary>
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

    public class RevokeSessionsResultDto
    {
        public int Revoked { get; set; }
    }
}
