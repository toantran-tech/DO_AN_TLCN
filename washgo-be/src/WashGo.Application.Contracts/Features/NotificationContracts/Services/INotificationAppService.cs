using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.NotificationContracts.Queries;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Services
{
    public interface INotificationAppService : IApplicationService
    {
        Task<PaginatedResult<NotificationDto>> GetMineAsync(int page, int take, bool unreadOnly);
        Task<BaseResponse<NotificationPageDto>> FilterAsync(NotificationFilterQuery query);
        Task<BaseResponse<int>> GetUnreadCountAsync();
        Task<BaseResponse<NotificationDto>> MarkReadAsync(Guid id);
        Task<BaseResponse<int>> MarkAllReadAsync();
        Task<BaseResponse<bool>> DeleteAsync(Guid id);
        Task<BaseResponse<int>> DeleteAllAsync();
    }
}
