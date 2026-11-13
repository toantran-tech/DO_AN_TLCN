using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using System;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Results;

namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Services
{
    public interface ISystemSettingAppService : IApplicationService
    {
        Task<BaseResponse<SystemSettingDto>> GetAsync();
        Task<BaseResponse<AdministrativeOperationDto>> GetOperationAsync(Guid id);
        Task<PaginatedResult<AuditLogDto>> GetHistoryAsync(AuditLogFilterQuery query);
        Task<BaseResponse<SystemSettingDto>> UpdateSlaAsync(UpdateSlaSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdateCommissionAsync(UpdateCommissionSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdatePenaltyAsync(UpdatePenaltySettingsCommand command);
    }
}
