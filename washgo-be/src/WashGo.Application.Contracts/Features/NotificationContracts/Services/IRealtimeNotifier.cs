using System;
using System.Threading.Tasks;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Services
{
    /// <summary>Đẩy thông báo tới client đang kết nối; được cài đặt bằng SignalR ở tầng HttpApi.</summary>
    public interface IRealtimeNotifier
    {
        Task SendAsync(Guid userId, NotificationDto notification);
        Task SendStateAsync(Guid userId, NotificationStateDto state);
        /// <summary>Ends every open real-time connection of the user (account locked/deleted); returns how many were closed.</summary>
        Task<int> DisconnectUserAsync(Guid userId);
    }
}
