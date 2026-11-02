using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;

namespace WashGo.HttpApi.Hubs
{
    public class SignalRRealtimeNotifier : IRealtimeNotifier, ITransientDependency
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRRealtimeNotifier(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task SendAsync(Guid userId, NotificationDto notification) =>
            _hub.Clients.User(userId.ToString()).SendAsync(NotificationHub.ReceiveMethod, notification);
    }
}
