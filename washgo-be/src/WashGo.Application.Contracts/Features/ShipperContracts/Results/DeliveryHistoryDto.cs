using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>UC 4.3.6: một lượt lấy/trả đồ Shipper đã hoàn thành.</summary>
    public class DeliveryHistoryDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DeliveryTaskType TaskType { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public string? BoxNumber { get; set; }
        /// <summary>Thời gian lấy đồ khỏi tủ (Collect) hoặc đặt đồ vào tủ (Return).</summary>
        public DateTime CompletedAt { get; set; }
        public WashOrderStatus OrderStatus { get; set; }
    }
}
