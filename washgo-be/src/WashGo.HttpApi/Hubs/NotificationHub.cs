using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WashGo.HttpApi.Hubs
{
    /// <summary>
    /// Kênh real-time một chiều server → client. SignalR nhận diện người dùng bằng claim NameIdentifier
    /// trong JWT, nên server gửi theo user id mà không cần client tự đăng ký nhóm.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string Path = "/hubs/notifications";
        public const string ReceiveMethod = "notification";
        /// <summary>Đọc/xóa trên một thiết bị được đẩy tới các thiết bị khác của cùng tài khoản.</summary>
        public const string StateMethod = "notificationState";
    }
}
