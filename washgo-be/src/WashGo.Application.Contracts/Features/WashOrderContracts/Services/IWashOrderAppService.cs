using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Services
{
    public interface IWashOrderAppService : IApplicationService
    {
        Task<PaginatedResult<WashOrderResultDto>> GetListAsync(WashOrderFilterQuery query);
        Task<PaginatedResult<WashOrderResultDto>> GetMyOrdersAsync(WashOrderFilterQuery query);
        Task<BaseResponse<WashOrderDetailDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<WashOrderDetailDto>> CreateAsync(CreateWashOrderCommand command);
        Task<BaseResponse<WashOrderDetailDto>> CancelAsync(Guid orderId, CancelWashOrderCommand command);
        Task<BaseResponse<WashOrderDetailDto>> UpdateStatusAsync(UpdateWashOrderStatusCommand command);
        Task<BaseResponse<WashOrderDetailDto>> UpdateActualWeightAsync(Guid orderId, UpdateActualWeightCommand command);
        Task<BaseResponse<WashOrderDetailDto>> MoveToStorageAsync(MoveOrderToStorageCommand command);
        Task<BaseResponse<WashOrderDetailDto>> AssignShipperAsync(AssignShipperCommand command);
        Task<BaseResponse<List<AvailableShipperDto>>> GetAvailableShippersAsync(Guid orderId);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashOrderFilterQuery query);
    }
}
