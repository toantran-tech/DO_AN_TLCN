using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Application.Contracts.Features.StorageContracts.Commands;
using WashGo.Application.Contracts.Features.StorageContracts.Queries;
using WashGo.Application.Contracts.Features.StorageContracts.Results;
namespace WashGo.Application.Contracts.Features.StorageContracts.Services;
public interface IStorageAppService : IApplicationService
{
    Task<PaginatedResult<StorageCycleDto>> FilterAsync(StorageFilterQuery query);
    Task<BaseResponse<Dictionary<string,int>>> SummaryAsync();
    Task<PaginatedResult<StorageCycleDto>> GetAsync(Guid orderId, int page, int take);
    Task<BaseResponse<List<AvailableShipperDto>>> GetShippersAsync(Guid orderId);
    Task<BaseResponse<StorageQuoteDto>> QuoteAsync(Guid orderId, Guid lockerId);
    Task<BaseResponse<StorageCycleDto>> ExecuteAsync(Guid orderId, string action, StorageCommand command);
    Task<BaseResponse<StorageCycleDto>> AssignAsync(Guid orderId, AssignStorageShipperCommand command);
    Task<BaseResponse<StorageCycleDto>> RequestReturnAsync(Guid orderId, RequestStorageReturnCommand command);
    Task<BaseResponse<StorageCycleDto>> ConfirmAsync(Guid orderId, string action, StorageHandoverCommand command);
    Task<BaseResponse<StorageCycleDto>> GetOperationAsync(Guid orderId, Guid operationId);
}
