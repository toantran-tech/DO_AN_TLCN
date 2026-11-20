using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
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

        // The JWT is checked only when a connection opens; a locked or deleted account's open sockets are aborted here.
        private static readonly ConcurrentDictionary<string, (string UserId, HubCallerContext Context)> Connections = new();

        public override Task OnConnectedAsync()
        {
            if (Context.UserIdentifier != null)
                Connections[Context.ConnectionId] = (Context.UserIdentifier, Context);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Connections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>Aborts every connection of the user on this server instance.</summary>
        public static int AbortUser(string userId)
        {
            var closed = 0;
            foreach (var (id, connection) in Connections)
            {
                if (connection.UserId != userId) continue;
                connection.Context.Abort();
                Connections.TryRemove(id, out _);
                closed++;
            }
            return closed;
        }
    }
}
