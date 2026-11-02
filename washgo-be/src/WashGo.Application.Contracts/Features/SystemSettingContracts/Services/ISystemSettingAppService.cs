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
        Task<BaseResponse<SystemSettingDto>> UpdateSlaAsync(UpdateSlaSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdateCommissionAsync(UpdateCommissionSettingsCommand command);
        Task<BaseResponse<SystemSettingDto>> UpdatePenaltyAsync(UpdatePenaltySettingsCommand command);
    }
}
