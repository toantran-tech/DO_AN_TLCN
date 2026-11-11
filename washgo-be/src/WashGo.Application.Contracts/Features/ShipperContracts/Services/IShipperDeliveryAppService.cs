using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Services
{
    public interface IShipperDeliveryAppService : IApplicationService
    {
        Task<PaginatedResult<DeliveryHistoryDto>> GetHistoryAsync(DeliveryHistoryQuery query);
        Task<BaseResponse<DeliverySummaryDto>> GetSummaryAsync(DeliveryHistoryQuery query);
        Task<BaseResponse<DeliveryHistoryDto>> GetEventAsync(Guid eventId);
        Task<BaseResponse<List<DeliveryLockerDto>>> GetLockersAsync(DeliveryHistoryQuery query);
        Task<BaseResponse<string>> ExportAsync(DeliveryHistoryQuery query);
    }
}
