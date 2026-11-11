using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>UC 4.3.6: một lượt lấy/trả đồ Shipper đã hoàn thành.</summary>
    public class DeliveryHistoryDto
    {
        public Guid EventId { get; set; }
        public Guid TaskId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DeliveryTaskType TaskType { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public string? BoxNumber { get; set; }
        /// <summary>Thời gian lấy đồ khỏi tủ (Collect) hoặc đặt đồ vào tủ (Return).</summary>
        public DateTime CompletedAt { get; set; }
        public WashOrderStatus OrderStatus { get; set; }
        public WashOrderStatus? EventStatus { get; set; }
        public WashOrderStatus? CurrentOrderStatus { get; set; }
        public string? MerchantOrderCode { get; set; }
        public string? MerchantName { get; set; }
        public Guid? LockerId { get; set; }
        public string? LockerAddress { get; set; }
        public double? LockerLatitude { get; set; }
        public double? LockerLongitude { get; set; }
        public List<string> LockerImageUrls { get; set; } = [];
        public string ImageBasis { get; set; } = "CurrentResource";
        public string? SnapshotSource { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? Deadline { get; set; }
        public int? SlaHours { get; set; }
        public string? Note { get; set; }
    }
}
