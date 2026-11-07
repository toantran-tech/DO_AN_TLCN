using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>Một nhiệm vụ lấy/trả đồ; mốc không có dữ liệu thật (đơn trước khi có bảng nhiệm vụ) là null.</summary>
    public class ShipperTaskDto
    {
        public Guid TaskId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string? MerchantOrderCode { get; set; }
        public DeliveryTaskType TaskType { get; set; }
        /// <summary>1 Assigned (đang làm), 2 Completed, 3 Reassigned, 4 Cancelled.</summary>
        public DeliveryTaskStatus Status { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        /// <summary>Tủ/ô của nhiệm vụ: lấy đồ = tủ gửi; trả đồ = tủ trả.</summary>
        public string LockerName { get; set; } = string.Empty;
        public string? BoxNumber { get; set; }
        public WashOrderStatus OrderStatus { get; set; }
    }
}
