using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Services;

public interface IMyShipperTaskAppService : IApplicationService
{
    Task<PaginatedResult<MyShipperTaskDto>> GetListAsync(MyShipperTaskQuery query);
    Task<BaseResponse<ShipperTaskSummaryDto>> GetSummaryAsync(MyShipperTaskQuery query);
    Task<BaseResponse<MyShipperTaskDto>> GetAsync(Guid taskId);
    Task<BaseResponse<ShipperTaskCredentialDto>> GetCredentialsAsync(Guid taskId);
    Task<BaseResponse<ShipperOperationDto>> GetOperationAsync(Guid taskId, Guid operationId);
}
