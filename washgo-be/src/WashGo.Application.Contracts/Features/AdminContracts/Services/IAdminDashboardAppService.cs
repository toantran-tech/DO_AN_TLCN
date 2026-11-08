using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AdminContracts.Queries;
using WashGo.Application.Contracts.Features.AdminContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AdminContracts.Services
{
    public interface IAdminDashboardAppService : IApplicationService
    {
        Task<BaseResponse<AdminDashboardDto>> GetDashboardAsync(AdminDashboardQuery query);
        Task<BaseResponse<AdminAlertsDto>> GetAlertsAsync(AdminAlertsQuery query);
        Task<PaginatedResult<AdminLookupItemDto>> LookupMerchantsAsync(AdminLookupQuery query);
        Task<PaginatedResult<AdminLookupItemDto>> LookupLockersAsync(AdminLookupQuery query);
    }
}
