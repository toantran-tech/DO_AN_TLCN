using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    public class WashOrderResultDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public Guid LockerId { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public Guid BoxId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public Guid? ReturnLockerId { get; set; }
        public Guid? ReturnBoxId { get; set; }
        public string? ReturnBoxNumber { get; set; }
        public Guid? ShipperId { get; set; }
        public DeliveryTaskType? AssignedTaskType { get; set; }
        public DateTime? ShipperAssignedAt { get; set; }
        public string? ShipperName { get; set; }
        public string? ShipperTaskPinCode { get; set; }
        public string? ShipperTaskQrString { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal? ActualWeightKg { get; set; }
        public decimal? IncludedWeightKg { get; set; }
        public decimal? ExcessWeightKg => ActualWeightKg.HasValue && IncludedWeightKg.HasValue
            ? Math.Max(0m, ActualWeightKg.Value - IncludedWeightKg.Value) : null;
        public decimal WeightSurchargeAmount { get; set; }
        public int? BaseProcessingHours { get; set; }
        public DateTime? EstimatedFinishAt { get; set; }
        public DateTime? EtaUpdatedAt { get; set; }
        public WashOrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string DepositPinCode { get; set; } = string.Empty;
        public string PickupPinCode { get; set; } = string.Empty;
        public string? PickupQrCodeString => string.IsNullOrEmpty(PickupPinCode)
            ? null : $"WASHGO:PICKUP:{OrderCode}:{PickupPinCode}";
        public string QrCodeString { get; set; } = string.Empty;
        public long ExpectedDeliveryTime { get; set; }
        public DateTime? DepositedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? PickupDeadline { get; set; }
        public decimal OverdueFeeTotal { get; set; }
        public decimal StorageFeeTotal { get; set; }
        public DateTime? MovedToStorageAt { get; set; }
        public string? StorageLocation { get; set; }
        public string? StorageReference { get; set; }
        public long CreatedOn { get; set; }
    }

    public class WashOrderDetailDto : WashOrderResultDto
    {
        public string? CustomerNote { get; set; }
        public List<WashOrderItemDto> Items { get; set; } = [];
        public List<WashOrderTimelineDto> Timeline { get; set; } = [];
    }

    public class WashOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public decimal? WeightKg { get; set; }
        public bool IsWeightBased { get; set; }
        public string? Note { get; set; }
    }

    public class WashOrderTimelineDto
    {
        public Guid Id { get; set; }
        public WashOrderStatus Status { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public long Timestamp { get; set; }
        public string? Note { get; set; }
    }

    public class OpenPickupBoxResultDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid ReturnLockerId { get; set; }
        public string ReturnLockerName { get; set; } = string.Empty;
        public Guid ReturnBoxId { get; set; }
        public string ReturnBoxNumber { get; set; } = string.Empty;
        public string PickupSessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
