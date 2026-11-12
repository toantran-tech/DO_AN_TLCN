using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Identity
{

    [SevagoTable(WashGoDbProperties.RefreshTokens, Schema = WashGoDbProperties.Schema)]
    public class RefreshToken : Entity<Guid>
    {
        public RefreshToken() { }
        public RefreshToken(Guid id) : base(id) { }

        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Thời điểm đăng nhập mở phiên; giữ nguyên qua các lần làm mới token (mỗi lần làm mới tạo dòng mới).</summary>
        public DateTime? StartedAt { get; set; }
        /// <summary>Id phiên logic, giữ nguyên qua các lần làm mới token (= Id của token đăng nhập đầu tiên).</summary>
        public Guid? SessionKey { get; set; }
        /// <summary>User-Agent của thiết bị (cắt 300 ký tự) để người dùng nhận ra phiên; không lưu IP.</summary>
        public string? UserAgent { get; set; }
    }
}
