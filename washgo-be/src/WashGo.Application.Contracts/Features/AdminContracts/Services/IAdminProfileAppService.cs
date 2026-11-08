using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AdminContracts.Commands;
using WashGo.Application.Contracts.Features.AdminContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AdminContracts.Services
{
    public interface IAdminProfileAppService : IApplicationService
    {
        Task<BaseResponse<AdminProfileDto>> GetAsync();
        Task<BaseResponse<AdminProfileDto>> UpdateAsync(UpdateAdminProfileCommand command);
    }
}
