using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AdminContracts.Queries
{
    /// <summary>
    /// Kỳ: "today", "week" (7 ngày gần nhất), "month" (tháng này, mặc định), "custom" (fromDate/toDate Unix giây) hoặc "all".
    /// Ngày được tính theo giờ Việt Nam; mọi mốc trả về là UTC (hậu tố Z).
    /// </summary>
    public class AdminDashboardQuery
    {
        public string? Period { get; set; } = "month";
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        /// <summary>Số Merchant trong bảng xếp hạng (1-20, mặc định 5).</summary>
        public int? TopMerchants { get; set; }
    }

    /// <summary>type/severity lọc danh sách; tổng theo loại luôn tính trên toàn hệ thống. page từ 1, take ≤ 100.</summary>
    public class AdminAlertsQuery
    {
        public int? Page { get; set; }
        public int? Take { get; set; }
        public AdminAlertType? Type { get; set; }
        public AdminAlertSeverity? Severity { get; set; }
    }

    public class AdminLookupQuery
    {
        public string? Keyword { get; set; }
        public int? Page { get; set; }
        public int? Take { get; set; }
        /// <summary>Lọc trạng thái (MerchantStatus hoặc LockerStatus theo API).</summary>
        public int? Status { get; set; }
    }
}
