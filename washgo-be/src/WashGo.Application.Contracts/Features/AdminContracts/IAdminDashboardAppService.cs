using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AdminContracts
{
    public interface IAdminDashboardAppService : IApplicationService
    {
        Task<BaseResponse<AdminDashboardDto>> GetDashboardAsync(AdminDashboardQuery query);
        Task<BaseResponse<AdminAlertsDto>> GetAlertsAsync(AdminAlertsQuery query);
        Task<PaginatedResult<AdminLookupItemDto>> LookupMerchantsAsync(AdminLookupQuery query);
        Task<PaginatedResult<AdminLookupItemDto>> LookupLockersAsync(AdminLookupQuery query);
    }
}
