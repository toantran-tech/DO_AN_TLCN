using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Results
{
    public class PromotionStatisticsDto
    {
        public Guid PromotionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int UsedCount { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        /// <summary>Tổng giá trị đơn trước giảm của các đơn dùng mã.</summary>
        public decimal TotalOrderAmount { get; set; }
        /// <summary>Doanh thu (giá cuối) của các đơn dùng mã, chưa bị hủy.</summary>
        public decimal Revenue { get; set; }
        /// <summary>Phần doanh thu đã thanh toán.</summary>
        public decimal PaidRevenue { get; set; }
        public List<PromotionDailyUsageDto> Daily { get; set; } = [];
    }

    public class PromotionDailyUsageDto
    {
        /// <summary>Ngày theo giờ Việt Nam (UTC+7), định dạng yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;
        public int UsedCount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Revenue { get; set; }
    }
}
