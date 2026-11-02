using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Services
{
    public interface IShipperProfileAppService : IApplicationService
    {
        Task<BaseResponse<ShipperProfileDto>> GetMyProfileAsync();
        Task<BaseResponse<ShipperProfileDto>> UpdateMyProfileAsync(UpdateShipperProfileCommand command);
    }
}
