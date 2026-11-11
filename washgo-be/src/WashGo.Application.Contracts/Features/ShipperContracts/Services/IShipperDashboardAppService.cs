using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Services;

public interface IShipperDashboardAppService : IApplicationService
{
    Task<BaseResponse<ShipperDashboardDto>> GetAsync(ShipperDashboardQuery query);
}
