using System;
using System.Threading.Tasks;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Services
{
    /// <summary>Đẩy thông báo tới client đang kết nối; được cài đặt bằng SignalR ở tầng HttpApi.</summary>
    public interface IRealtimeNotifier
    {
        Task SendAsync(Guid userId, NotificationDto notification);
    }
}
