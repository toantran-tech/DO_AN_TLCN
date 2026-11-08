using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Services
{
    public interface ISystemSettingAppService : IApplicationService
    {
        Task<BaseResponse<SystemSettingDto>> GetAsync();
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeOperationDto>> GetOperationAsync(System.Guid id);
        Task<PaginatedResult<WashGo.Application.Contracts.Features.AuditContracts.Results.AuditLogDto>> GetHistoryAsync(WashGo.Application.Contracts.Features.AuditContracts.Queries.AuditLogFilterQuery query);
        Task<BaseResponse<SystemSettingDto>> UpdateSlaAsync(UpdateSlaSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdateCommissionAsync(UpdateCommissionSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdatePenaltyAsync(UpdatePenaltySettingsCommand command);
    }
}
