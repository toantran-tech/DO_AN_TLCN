using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    /// <summary>
    /// Một nhiệm vụ lấy/trả đồ của Shipper: mốc phân công, hoàn tất/kết thúc thật sự đã ghi nhận.
    /// Dữ liệu trước khi có bảng được backfill; mốc không có dữ liệu để null, không suy đoán.
    /// </summary>
    [SevagoTable(WashGoDbProperties.DeliveryTasks, Schema = WashGoDbProperties.Schema)]
    public class DeliveryTask : Entity<Guid>
    {
        public DeliveryTask() { }
        public DeliveryTask(Guid id) : base(id) { }

        public Guid OrderId { get; set; }
        public Guid ShipperId { get; set; }
        public Guid? MerchantId { get; set; }
        public DeliveryTaskType TaskType { get; set; }
        public DeliveryTaskStatus Status { get; set; }
        public DateTime? AssignedAt { get; set; }
        public Guid? AssignedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        /// <summary>Thời điểm kết thúc khi bị phân công lại hoặc đơn bị hủy.</summary>
        public DateTime? EndedAt { get; set; }
        public Guid? CompletionEventId { get; set; }
        public Guid? CompletedBy { get; set; }
        public string? CompletedByRole { get; set; }
        public DateTime? SlaStartedAt { get; set; }
        public int? SlaHours { get; set; }
        public DateTime? Deadline { get; set; }
        /// <summary>CapturedAtCompletion or LegacyCurrentResource; null before completion.</summary>
        public string? SnapshotSource { get; set; }
        public string? OrderCodeSnapshot { get; set; }
        public string? MerchantOrderCodeSnapshot { get; set; }
        public string? MerchantNameSnapshot { get; set; }
        public Guid? LockerIdSnapshot { get; set; }
        public string? LockerNameSnapshot { get; set; }
        public string? LockerAddressSnapshot { get; set; }
        public double? LockerLatitudeSnapshot { get; set; }
        public double? LockerLongitudeSnapshot { get; set; }
        public Guid? BoxIdSnapshot { get; set; }
        public string? BoxNumberSnapshot { get; set; }
        public WashOrderStatus? OrderStatusSnapshot { get; set; }
        public string? CompletionNote { get; set; }
    }
}
