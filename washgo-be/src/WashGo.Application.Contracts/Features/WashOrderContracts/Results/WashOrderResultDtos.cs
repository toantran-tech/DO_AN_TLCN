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
        /// <summary>Phiên bản dữ liệu; gửi lại làm expectedRevision (hoặc If-Match) khi ghi để tránh ghi đè thay đổi mới hơn.</summary>
        public long Revision { get; set; }
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
        /// <summary>Dịch vụ của đơn theo ID kèm ảnh đại diện hiện tại (null khi dịch vụ không có ảnh).</summary>
        public List<OrderServiceRefDto> Services { get; set; } = [];
        /// <summary>Ảnh trạm gửi / trạm nhận (trạm nhận = returnLockerId, mặc định trạm gửi). Ảnh hiện tại, không phải snapshot.</summary>
        public List<string> LockerImageUrls { get; set; } = [];
        public string? LockerThumbnailUrl => LockerImageUrls.Count > 0 ? LockerImageUrls[0] : null;
        public List<string> ReturnLockerImageUrls { get; set; } = [];
        public string? ReturnLockerThumbnailUrl => ReturnLockerImageUrls.Count > 0 ? ReturnLockerImageUrls[0] : null;
        /// <summary>Số điện thoại và ảnh cửa hàng hiện tại của Merchant được phân công; null khi chưa phân công.</summary>
        public string? MerchantPhoneNumber { get; set; }
        public List<string> MerchantImageUrls { get; set; } = [];
        public string? MerchantThumbnailUrl => MerchantImageUrls.Count > 0 ? MerchantImageUrls[0] : null;
        /// <summary>Ảnh đại diện tài khoản khách (chỉ trả cho Admin).</summary>
        public string? CustomerAvatarUrl { get; set; }
        /// <summary>Liên hệ của Shipper đang được giao (chỉ Admin/Merchant của đơn); null khi chưa giao hoặc Shipper đã bị xóa.</summary>
        public string? ShipperAvatarUrl { get; set; }
        public string? ShipperPhone { get; set; }
        public string? ShipperEmail { get; set; }
        public VehicleType? ShipperVehicleType { get; set; }
        public string? ShipperVehiclePlate { get; set; }
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
        /// <summary>Tỷ lệ hoa hồng (%) chốt cố định theo đơn (UC 2.3.2, UC 3.8).</summary>
        public decimal CommissionRateApplied { get; set; }
        /// <summary>Số tiền tính hoa hồng = FinalAmount (tiền giặt sau giảm giá).</summary>
        public decimal CommissionBaseAmount => FinalAmount;
        /// <summary>Tiền hoa hồng hệ thống giữ (làm tròn số nguyên đồng).</summary>
        public decimal CommissionAmount => decimal.Round(FinalAmount * CommissionRateApplied / 100m, 0, MidpointRounding.AwayFromZero);
        /// <summary>Doanh thu thực nhận của tiệm giặt cho đơn này = CommissionBaseAmount - CommissionAmount.</summary>
        public decimal MerchantNetAmount => Math.Max(0m, CommissionBaseAmount - CommissionAmount);
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
        /// <summary>Hạn thanh toán (UTC), đặt khi đơn chuyển sang Chờ thanh toán; quá hạn thì đồ được chuyển về kho.</summary>
        public DateTime? PaymentDeadline { get; set; }
        /// <summary>Đơn chờ thanh toán, chưa trả tiền và đã quá hạn thanh toán.</summary>
        public bool IsPaymentOverdue => Status == WashOrderStatus.AwaitingPayment && PaymentStatus == PaymentStatus.Unpaid &&
            PaymentDeadline.HasValue && ServerNow > PaymentDeadline.Value;
        /// <summary>Số giây còn lại để thanh toán (chỉ khi đơn đang chờ thanh toán và chưa trả tiền).</summary>
        public long? PaymentSecondsLeft => Status == WashOrderStatus.AwaitingPayment && PaymentStatus == PaymentStatus.Unpaid &&
            PaymentDeadline.HasValue ? Math.Max(0, (long)(PaymentDeadline.Value - ServerNow).TotalSeconds) : null;
        /// <summary>Đồ đã ở tủ nhận nhưng khách chưa lấy và đã quá hạn lấy đồ (bắt đầu tính phí quá hạn).</summary>
        public bool IsPickupOverdue => Status == WashOrderStatus.ReadyToReturn && PickupDeadline.HasValue && ServerNow > PickupDeadline.Value;
        /// <summary>
        /// Các loại quá hạn đang áp dụng: DepositExpired (quá hạn gửi đồ), PaymentOverdue (quá hạn thanh toán),
        /// PickupOverdue (quá hạn lấy đồ). Rỗng khi đơn không quá hạn mốc nào.
        /// </summary>
        public List<string> OverdueTypes
        {
            get
            {
                var types = new List<string>();
                if (IsDepositExpired) types.Add("DepositExpired");
                if (IsPaymentOverdue) types.Add("PaymentOverdue");
                if (IsPickupOverdue) types.Add("PickupOverdue");
                return types;
            }
        }
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

        /// <summary>
        /// Ẩn danh thông tin cá nhân khách hàng đối với đối tác tiệm giặt (UC 2.3.2).
        /// </summary>
        public void RedactForMerchant()
        {
            CustomerId = Guid.Empty;
            CustomerName = "***";
            CustomerPhoneNumber = "***";
            DepositPinCode = string.Empty;
            PickupPinCode = string.Empty;
            QrCodeString = string.Empty;
            ShipperTaskPinCode = null;
        }

        /// <summary>Phần trăm các bước áp dụng cho đơn đã đi qua (bước hiện tại tính là đã tới); 100 khi hoàn tất.</summary>
        public int ProgressPercent { get; set; }
        /// <summary>Các bước quy trình: bước nào áp dụng theo dịch vụ của đơn, đã xong, đang ở, thời điểm (Unix giây).</summary>
        public List<OrderWorkflowStepDto> WorkflowSteps { get; set; } = [];
    }

    public class OrderWorkflowStepDto
    {
        public WashOrderStatus Status { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsApplicable { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public long? Timestamp { get; set; }
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
        /// <summary>
        /// ETA dự đoán (giặt xong, UTC) từ dữ liệu đơn thật. Khác `estimatedFinishAt` = ETA theo quy tắc (giờ xử lý chuẩn).
        /// </summary>
        public DateTime? PredictedFinishAt { get; set; }
        /// <summary>predicted (dự đoán từ dữ liệu), rule (chưa đủ dữ liệu, dùng ETA quy tắc), done (đã giặt xong), notApplicable (đơn hủy).</summary>
        public string PredictionStatus { get; set; } = "notApplicable";
        /// <summary>history-merchant (cùng tiệm, cùng loại dịch vụ), history-service (toàn hệ thống, cùng loại), rule, actual.</summary>
        public string? PredictionSource { get; set; }
        public int? PredictionSampleSize { get; set; }
        /// <summary>Hệ số tải của tiệm (1 = bình thường, tối đa 1.5).</summary>
        public double? PredictionLoadFactor { get; set; }
        public DateTime? PredictionUpdatedAt { get; set; }
        /// <summary>Thao tác Merchant/Admin được làm lúc này (status/cân/phân công) kèm lý do; null với Customer, Shipper.</summary>
        public OrderAllowedActionsDto? AllowedActions { get; set; }
        public List<WashOrderItemDto> Items { get; set; } = [];
        public List<WashOrderTimelineDto> Timeline { get; set; } = [];
    }

    public class OrderServiceRefDto
    {
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? ServiceThumbnailUrl { get; set; }
    }

    /// <summary>Mã mở tủ / mã nhiệm vụ của một đơn: chỉ Admin xem qua endpoint riêng, mỗi lần xem được ghi lại.</summary>
    public class OrderAccessCodesDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string DepositPinCode { get; set; } = string.Empty;
        public string QrCodeString { get; set; } = string.Empty;
        public string PickupPinCode { get; set; } = string.Empty;
        public string? PickupQrCodeString { get; set; }
        public string? ShipperTaskPinCode { get; set; }
        public string? ShipperTaskQrString { get; set; }
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
        /// <summary>Ảnh hiện tại của dịch vụ (kể cả dịch vụ đã ngừng/xóa mềm).</summary>
        public List<string> ServiceImageUrls { get; set; } = [];
        public string? ServiceThumbnailUrl => ServiceImageUrls.Count > 0 ? ServiceImageUrls[0] : null;
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
