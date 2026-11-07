using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    public class WashOrderResultDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        /// <summary>Mã đơn nội bộ của Merchant (UC 2.3.1).</summary>
        public string? MerchantOrderCode { get; set; }
        /// <summary>QR nhận diện đơn, không chứa mã mở tủ; dùng cho Merchant/Shipper đối chiếu túi đồ.</summary>
        public string OrderQrString => $"WASHGO:ORDER:{OrderCode}";
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public Guid LockerId { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public Guid BoxId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize? BoxSize { get; set; }
        /// <summary>Tên các dịch vụ của đơn (danh sách đơn của Customer/Merchant).</summary>
        public List<string> ServiceNames { get; set; } = [];
        /// <summary>Đơn mới cần xử lý để highlight: Merchant = đã gửi đồ, chưa phân công Shipper; Shipper = nhiệm vụ chưa làm.</summary>
        public bool IsNew { get; set; }
        public Guid? ReturnLockerId { get; set; }
        public Guid? ReturnBoxId { get; set; }
        public string? ReturnBoxNumber { get; set; }
        public Guid? ShipperId { get; set; }
        public DeliveryTaskType? AssignedTaskType { get; set; }
        public DateTime? ShipperAssignedAt { get; set; }
        public string? ShipperNote { get; set; }
        public string? ShipperName { get; set; }
        public string? ShipperTaskPinCode { get; set; }
        public string? ShipperTaskQrString { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PromotionCode { get; set; }
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
        /// <summary>Hạn gửi đồ (UTC), chốt lúc tạo đơn; cùng mốc với kiểm tra gửi đồ và phí hủy.</summary>
        public DateTime? DepositDeadline { get; set; }
        /// <summary>Thời gian giữ ô (phút) áp dụng cho đơn này.</summary>
        public int? DepositWindowMinutes => DepositDeadline.HasValue
            ? (int)Math.Round((DepositDeadline.Value - DateTimeOffset.FromUnixTimeSeconds(CreatedOn).UtcDateTime).TotalMinutes) : null;
        /// <summary>Giờ server (UTC) lúc trả kết quả, để FE đếm ngược đúng dù đồng hồ máy khách lệch.</summary>
        public DateTime ServerNow => DateTime.UtcNow;
        /// <summary>Đơn còn chờ gửi nhưng đã quá hạn: không gửi được nữa, nên hủy để giải phóng ô.</summary>
        public bool IsDepositExpired => Status == WashOrderStatus.Pending && DepositDeadline.HasValue && ServerNow > DepositDeadline.Value;
        /// <summary>Số giây còn lại để gửi đồ (chỉ khi đơn đang chờ gửi).</summary>
        public long? DepositSecondsLeft => Status == WashOrderStatus.Pending && DepositDeadline.HasValue
            ? Math.Max(0, (long)(DepositDeadline.Value - ServerNow).TotalSeconds) : null;
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
        public decimal CancelFeeAmount { get; set; }
        public decimal CancelFeePaidAmount { get; set; }
        public decimal CancellationFeeOutstanding => Math.Max(0m, CancelFeeAmount - CancelFeePaidAmount);
        public decimal CancellationRefundAmount { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public WashOrderStatus? CancellationFromStatus { get; set; }
        public CancelledGoodsStatus CancelledGoodsStatus { get; set; }
        public DateTime? CancelledGoodsReturnedAt { get; set; }
    }

    public class WashOrderDetailDto : WashOrderResultDto
    {
        public string? CustomerNote { get; set; }
        /// <summary>UC 4.3.2: locker/ô của nhiệm vụ hiện tại (lấy đồ: tủ gửi; trả đồ: tủ trả) và nút hành động.</summary>
        public string? TaskLockerName { get; set; }
        public string? TaskLockerAddress { get; set; }
        public string? TaskBoxNumber { get; set; }
        /// <summary>Collect / Return khi Shipper còn nhiệm vụ chưa làm; null khi không có.</summary>
        public DeliveryTaskType? NextAction { get; set; }
        /// <summary>Tủ khách nhận đồ (tủ trả; mặc định là tủ gửi) để chỉ đường trước khi mở phiên nhận đồ.</summary>
        public string? ReturnLockerName { get; set; }
        public string? ReturnLockerAddress { get; set; }
        public double? ReturnLockerLatitude { get; set; }
        public double? ReturnLockerLongitude { get; set; }
        /// <summary>Địa chỉ và tọa độ tủ gửi đồ.</summary>
        public string? LockerAddress { get; set; }
        public double? LockerLatitude { get; set; }
        public double? LockerLongitude { get; set; }
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
