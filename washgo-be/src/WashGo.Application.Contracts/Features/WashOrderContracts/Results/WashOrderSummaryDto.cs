using System;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    /// <summary>
    /// Thống kê đơn theo cùng bộ lọc của danh sách (keyword, paymentStatus, fromDate/toDate...), không áp dụng
    /// status, phân trang, sắp xếp. Đếm bằng một truy vấn nên tổng và từng trạng thái cùng một thời điểm.
    /// </summary>
    public class WashOrderSummaryDto
    {
        public int Total { get; set; }
        public MerchantStatusCountsDto StatusCounts { get; set; } = new();
        /// <summary>Khoảng ngày tạo đơn đã áp dụng (Unix giây, 0 = không giới hạn).</summary>
        public long FromDate { get; set; }
        public long ToDate { get; set; }
        /// <summary>Thời điểm lấy số liệu (UTC).</summary>
        public DateTime GeneratedAt { get; set; }
    }
}
