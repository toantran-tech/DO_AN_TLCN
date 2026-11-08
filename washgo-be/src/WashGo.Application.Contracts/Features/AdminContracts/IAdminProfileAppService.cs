using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AdminContracts
{
    public interface IAdminProfileAppService : IApplicationService
    {
        Task<BaseResponse<AdminProfileDto>> GetAsync();
        Task<BaseResponse<AdminProfileDto>> UpdateAsync(UpdateAdminProfileCommand command);
    }
}
