using System;
using System.Collections.Generic;
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
        Task<BaseResponse<ShipperSummaryDto>> GetSummaryAsync(Guid? merchantId);
        Task<BaseResponse<ShipperDto>> CreateAsync(CreateShipperCommand command);
        Task<BaseResponse<ShipperDto>> UpdateAsync(Guid id, UpdateShipperCommand command);
        Task<BaseResponse<ShipperDto>> LockAsync(Guid id, ShipperAccountActionCommand command);
        Task<BaseResponse<ShipperDto>> UnlockAsync(Guid id, ShipperAccountActionCommand command);
        Task<BaseResponse<List<ShipperAuditDto>>> GetAuditsAsync(Guid id);
        Task<BaseResponse<ShipperDto>> DeleteAsync(Guid id, string? reason);
        Task<PaginatedResult<ShipperTaskDto>> GetTasksAsync(Guid id, ShipperTaskQuery query);
        Task<BaseResponse<ShipperBatchResultDto>> BatchLockAsync(ShipperBatchActionCommand command);
        Task<BaseResponse<ShipperBatchResultDto>> BatchDeleteAsync(ShipperBatchActionCommand command);
    }
}
