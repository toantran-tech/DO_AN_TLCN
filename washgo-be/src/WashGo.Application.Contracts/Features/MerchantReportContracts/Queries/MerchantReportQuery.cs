namespace WashGo.Application.Contracts.Features.MerchantReportContracts.Queries
{
    /// <summary>
    /// Kỳ báo cáo Merchant, giờ Việt Nam (Asia/Ho_Chi_Minh, UTC+7).
    /// period: today | week (thứ Hai → hiện tại) | month (mùng 1 → hiện tại) | custom (cần from, to dạng yyyy-MM-dd, tính cả hai ngày, tối đa 366 ngày).
    /// granularity: hour | day | week | month; bỏ trống thì BE chọn theo độ dài kỳ (tối đa 400 điểm).
    /// </summary>
    public class MerchantReportQuery
    {
        public string? Period { get; set; } = "month";
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Granularity { get; set; }
        /// <summary>Số dịch vụ bán chạy trả về (1–20, mặc định 5).</summary>
        public int? Top { get; set; }
    }
}
