using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    /// <summary>
    /// Thao tác Merchant/Admin được làm trên đơn ngay lúc đọc, tính bằng cùng luật BE dùng khi ghi.
    /// Server vẫn kiểm tra lại khi ghi; mã lý do (reasonCode) ổn định để FE dịch.
    /// </summary>
    public class OrderAllowedActionsDto
    {
        /// <summary>Các trạng thái được gửi qua PUT /wash-order/status lúc này.</summary>
        public List<WashOrderStatus> AllowedNextStatuses { get; set; } = [];
        /// <summary>Từng bước xử lý (Washing…AwaitingPayment) kèm lý do khi chưa được chuyển.</summary>
        public List<OrderStatusOptionDto> StatusOptions { get; set; } = [];
        public bool CanUpdateWeight { get; set; }
        /// <summary>RoleNotAllowed, WrongStage, LegacyPricing, NoWeightService.</summary>
        public string? UpdateWeightReasonCode { get; set; }
        public string? UpdateWeightReason { get; set; }
        /// <summary>Đã có cân nặng thực tế (cân lại vẫn được khi CanUpdateWeight).</summary>
        public bool WeightAlreadySet { get; set; }
        public bool CanAssignShipper { get; set; }
        /// <summary>Nhiệm vụ sẽ tạo khi phân công: Collect (lấy đồ) hoặc Return (trả đồ).</summary>
        public DeliveryTaskType? AssignTaskType { get; set; }
        /// <summary>RoleNotAllowed, NoMerchant, WrongStage.</summary>
        public string? AssignShipperReasonCode { get; set; }
        public string? AssignShipperReason { get; set; }
        /// <summary>Nhiệm vụ hiện tại đã có Shipper; phân công nữa là đổi người.</summary>
        public bool HasAssignedShipper { get; set; }
        /// <summary>Ghi nhận chuyển đồ quá hạn từ tủ trả sang kho (Admin/Shipper).</summary>
        public bool CanMoveToStorage { get; set; }
        /// <summary>RoleNotAllowed, WrongStage, AlreadyMoved, NotYetDue.</summary>
        public string? MoveToStorageReasonCode { get; set; }
        public string? MoveToStorageReason { get; set; }
        /// <summary>Thời điểm (UTC) được phép chuyển kho = hạn nhận đồ + số giờ cấu hình; null khi chưa có hạn nhận đồ.</summary>
        public DateTime? TransferToStorageEligibleAt { get; set; }
    }

    public class OrderStatusOptionDto
    {
        public WashOrderStatus Status { get; set; }
        public bool Allowed { get; set; }
        /// <summary>RoleNotAllowed, WeightRequired, InvalidTransition.</summary>
        public string? ReasonCode { get; set; }
        public string? Reason { get; set; }
    }
}
