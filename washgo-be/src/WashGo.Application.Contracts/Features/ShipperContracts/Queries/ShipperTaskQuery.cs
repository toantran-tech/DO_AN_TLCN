using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries
{
    /// <summary>Lịch sử nhiệm vụ của một Shipper (Merchant/Admin): lọc loại và trạng thái nhiệm vụ, phân trang tại BE.</summary>
    public class ShipperTaskQuery
    {
        public DeliveryTaskType? TaskType { get; set; }
        public DeliveryTaskStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
