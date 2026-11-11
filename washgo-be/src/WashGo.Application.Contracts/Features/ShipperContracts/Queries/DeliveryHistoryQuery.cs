using WashGo.Domain.Shared.Enums;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries
{
    /// <summary>UC 4.3.6: lọc lịch sử giao nhận theo thời gian hoàn thành (Unix giây) và loại nhiệm vụ.</summary>
    public class DeliveryHistoryQuery : BaseFilterQuery
    {
        public DeliveryHistoryQuery() { Page = 1; Take = 20; }
        public DeliveryTaskType? TaskType { get; set; }
        public Guid? LockerId { get; set; }
        public bool? CancelledReturn { get; set; }
        public WashOrderStatus? EventStatus { get; set; }
    }
}
