using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results
{
    /// <summary>
    /// Báo cáo doanh thu và tăng trưởng của Merchant (UC 2.2.3).
    /// </summary>
    public class MerchantRevenueReportDto
    {
        /// <summary>Kỳ báo cáo áp dụng: "today", "week", "month", "custom", "all".</summary>
        public string Period { get; set; } = "month";

        /// <summary>Mốc bắt đầu (theo giờ VN UTC+7).</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Mốc kết thúc (theo giờ VN UTC+7).</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>Tổng doanh thu tiền giặt các đơn đã thanh toán trong kỳ.</summary>
        public decimal TotalGrossRevenue { get; set; }

        /// <summary>Tổng phí hoa hồng hệ thống giữ lại theo snapshot từng đơn.</summary>
        public decimal TotalCommission { get; set; }

        /// <summary>Doanh thu thực nhận của tiệm giặt = TotalGrossRevenue - TotalCommission.</summary>
        public decimal TotalPayoutAmount { get; set; }

        /// <summary>Tổng tiền hoàn trả cho khách hàng (nếu có đơn phát sinh hoàn tiền).</summary>
        public decimal RefundedAmount { get; set; }

        /// <summary>Số lượng đơn hàng đã thanh toán trong kỳ.</summary>
        public int PaidOrderCount { get; set; }

        /// <summary>Số lượng đơn hàng bị hủy trong kỳ.</summary>
        public int CancelledOrderCount { get; set; }

        /// <summary>Tỷ lệ tăng trưởng doanh thu thực nhận so với kỳ trước tương đương (%), null nếu kỳ trước không có doanh thu.</summary>
        public decimal? GrowthRate { get; set; }

        /// <summary>Doanh thu thực nhận của kỳ trước tương ứng để đối chiếu so sánh.</summary>
        public decimal? PreviousPayoutAmount { get; set; }

        /// <summary>Chuỗi dữ liệu chi tiết doanh thu theo từng ngày trong kỳ.</summary>
        public List<MerchantDailyRevenueDto> DailySeries { get; set; } = [];

        /// <summary>Thời điểm tạo báo cáo trên server (theo giờ VN UTC+7).</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class MerchantDailyRevenueDto
    {
        public DateTime Date { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal Commission { get; set; }
        public decimal Payout { get; set; }
        public int OrderCount { get; set; }
    }
}

