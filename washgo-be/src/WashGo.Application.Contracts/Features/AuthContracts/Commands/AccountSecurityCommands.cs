namespace WashGo.Application.Contracts.Features.AuthContracts.Commands
{
    /// <summary>Đổi mật khẩu khi đang đăng nhập: bắt buộc mật khẩu hiện tại.</summary>
    public class ChangePasswordCommand
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
        /// <summary>true (mặc định): đăng xuất mọi phiên khác, giữ phiên hiện tại.</summary>
        public bool RevokeOtherSessions { get; set; } = true;
    }
}
