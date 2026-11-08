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

        /// <summary>Mốc bắt đầu kỳ (UTC, có hậu tố Z).</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Mốc kết thúc kỳ (UTC, có hậu tố Z).</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>Tiền giặt khách đã thanh toán thành công trong kỳ (theo ngày giao dịch).</summary>
        public decimal TotalGrossRevenue { get; set; }

        /// <summary>Tổng phí hoa hồng hệ thống giữ lại theo snapshot từng đơn.</summary>
        public decimal TotalCommission { get; set; }

        /// <summary>Thực nhận = tiền giặt − tiền hoàn − hoa hồng (theo tỷ lệ chốt từng đơn; xem CommissionComplete).</summary>
        public decimal TotalPayoutAmount { get; set; }

        /// <summary>Tiền đã hoàn cho khách trong kỳ (theo ngày hoàn).</summary>
        public decimal RefundedAmount { get; set; }

        /// <summary>Số lượng đơn hàng đã thanh toán trong kỳ.</summary>
        public int PaidOrderCount { get; set; }

        /// <summary>Số lượng đơn hàng bị hủy trong kỳ.</summary>
        public int CancelledOrderCount { get; set; }

        /// <summary>% thay đổi thực nhận so với kỳ trước; null khi kỳ trước bằng 0 (xem IsNewRevenue) hoặc hoa hồng chưa tính đủ.</summary>
        public decimal? GrowthRate { get; set; }
        /// <summary>Kỳ trước không có thực nhận nhưng kỳ này có: phát sinh mới, không có % tăng trưởng.</summary>
        public bool IsNewRevenue { get; set; }
        /// <summary>false khi có đơn chưa có tỷ lệ hoa hồng chốt: hoa hồng/thực nhận chỉ là tạm tính.</summary>
        public bool CommissionComplete { get; set; } = true;
        public int OrdersWithoutCommissionSnapshot { get; set; }

        /// <summary>Doanh thu thực nhận của kỳ trước tương ứng để đối chiếu so sánh.</summary>
        public decimal? PreviousPayoutAmount { get; set; }

        /// <summary>Chuỗi dữ liệu chi tiết doanh thu theo từng ngày trong kỳ.</summary>
        public List<MerchantDailyRevenueDto> DailySeries { get; set; } = [];

        /// <summary>Thời điểm tạo báo cáo trên server (UTC, hậu tố Z; FE đổi sang giờ Việt Nam).</summary>
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

