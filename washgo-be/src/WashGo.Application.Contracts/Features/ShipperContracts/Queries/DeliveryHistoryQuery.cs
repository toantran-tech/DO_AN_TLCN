using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries
{
    /// <summary>UC 4.3.6: lọc lịch sử giao nhận theo thời gian hoàn thành (Unix giây) và loại nhiệm vụ.</summary>
    public class DeliveryHistoryQuery
    {
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        public DeliveryTaskType? TaskType { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
