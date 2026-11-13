using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Queries
{
    public class MerchantFilterQuery : BaseFilterQuery
    {
        public MerchantStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public Guid? ServiceAreaId { get; set; }
    }

    /// <summary>
    /// Tham số lọc thời gian cho Merchant Dashboard (UC 2.2.1).
    /// </summary>
    public class MerchantDashboardQuery
    {
        /// <summary>
        /// Kỳ lọc thời gian: "today" (hôm nay), "week" (7 ngày qua), "month" (tháng này), "custom" (tùy chọn khoảng ngày), hoặc "all" (toàn bộ lịch sử). Mặc định là "all".
        /// </summary>
        public string? Period { get; set; } = "all";

        /// <summary>
        /// Mốc bắt đầu (Unix timestamp tính bằng giây) khi chọn Period = "custom".
        /// </summary>
        public long? FromDate { get; set; }

        /// <summary>
        /// Mốc kết thúc (Unix timestamp tính bằng giây) khi chọn Period = "custom".
        /// </summary>
        public long? ToDate { get; set; }
    }

    /// <summary>
    /// Tham số lọc thời gian cho Báo cáo doanh thu & tăng trưởng Merchant (UC 2.2.3).
    /// </summary>
    public class MerchantRevenueReportQuery
    {
        /// <summary>
        /// Kỳ báo cáo: "today", "week" (7 ngày qua), "month" (tháng này), "custom" (tùy chọn khoảng ngày), hoặc "all". Mặc định là "month".
        /// </summary>
        public string? Period { get; set; } = "month";

        /// <summary>Mốc bắt đầu (Unix timestamp tính bằng giây) khi chọn Period = "custom".</summary>
        public long? FromDate { get; set; }

        /// <summary>Mốc kết thúc (Unix timestamp tính bằng giây) khi chọn Period = "custom".</summary>
        public long? ToDate { get; set; }
    }

    /// <summary>Phân trang danh sách cảnh báo SLA; alertType lọc một loại (1 lấy đồ, 2 giặt sấy, 3 trả đồ).</summary>
    public class MerchantSlaAlertsQuery
    {
        public int? Page { get; set; }
        public int? Take { get; set; }
        public MerchantSlaAlertType? AlertType { get; set; }
    }
}
