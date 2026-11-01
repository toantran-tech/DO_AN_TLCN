using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Services
{
    public interface IShipperManagementAppService : IApplicationService
    {
        Task<PaginatedResult<ShipperDto>> GetListAsync(ShipperFilterQuery query);
        Task<BaseResponse<ShipperDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<ShipperDto>> CreateAsync(CreateShipperCommand command);
        Task<BaseResponse<ShipperDto>> UpdateAsync(Guid id, UpdateShipperCommand command);
    }
}
