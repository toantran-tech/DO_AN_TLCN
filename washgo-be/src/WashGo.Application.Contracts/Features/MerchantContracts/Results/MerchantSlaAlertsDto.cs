using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results
{
    /// <summary>
    /// Tổng hợp cảnh báo vi phạm SLA cho Merchant (UC 2.2.2 - Overdue Attention).
    /// </summary>
    public class MerchantSlaAlertsDto
    {
        /// <summary>Tổng số lượng đơn cần tiệm chú ý xử lý khẩn cấp.</summary>
        public int TotalAlerts => CollectionAlertsCount + ProcessingAlertsCount + ReturnAlertsCount;

        /// <summary>Số đơn quá hạn lấy đồ (đơn Deposited > thời gian SLA kể từ lúc khách gửi đồ).</summary>
        public int CollectionAlertsCount { get; set; }

        /// <summary>Số đơn quá hạn xử lý giặt sấy (đơn đang giặt/sấy/ủi/xếp đã quá EstimatedFinishAt).</summary>
        public int ProcessingAlertsCount { get; set; }

        /// <summary>Số đơn quá hạn trả đồ (đơn Paid > thời gian SLA kể từ lúc thanh toán mà chưa trả đồ vào tủ).</summary>
        public int ReturnAlertsCount { get; set; }

        /// <summary>Danh sách các đơn ưu tiên xử lý khẩn cấp xếp theo số phút quá hạn giảm dần.</summary>
        public List<MerchantPriorityOrderDto> PriorityOrders { get; set; } = [];

        /// <summary>Thời điểm server hiện tại (theo giờ VN UTC+7).</summary>
        public DateTime ServerNow { get; set; }
    }

    public class MerchantPriorityOrderDto
    {
        private string? _suggestedAction;

        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string? MerchantOrderCode { get; set; }
        public WashOrderStatus Status { get; set; }

        /// <summary>Phân loại vi phạm SLA: CollectionOverdue, ProcessingOverdue, ReturnOverdue.</summary>
        public MerchantSlaAlertType AlertType { get; set; }

        /// <summary>Mã hành động gợi ý xử lý tương ứng.</summary>
        public MerchantSlaActionType ActionType => AlertType.GetSuggestedActionType();

        /// <summary>Mốc hạn chót SLA (theo giờ VN UTC+7).</summary>
        public DateTime Deadline { get; set; }

        /// <summary>Số phút đã quá hạn tính đến hiện tại.</summary>
        public int OverdueMinutes { get; set; }

        /// <summary>Thao tác gợi ý cho tiệm giặt (tự động suy diễn theo AlertType hoặc ghi đè tùy chọn).</summary>
        public string SuggestedAction
        {
            get => _suggestedAction ?? AlertType.GetSuggestedActionDescription();
            set => _suggestedAction = value;
        }

        public string LockerName { get; set; } = string.Empty;
        public string BoxNumber { get; set; } = string.Empty;
    }
}
