using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Results
{
    public class RevenueReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>Tiền giặt + phí quá hạn/lưu kho + phí hủy đã thu, trừ tiền đã hoàn trong kỳ.</summary>
        public decimal TotalRevenue { get; set; }
        public decimal LaundryRevenue { get; set; }
        public decimal StorageFeeRevenue { get; set; }
        public decimal CancellationFeeRevenue { get; set; }
        public decimal RefundedAmount { get; set; }
        public int PaidOrderCount { get; set; }
        public List<MerchantRevenueDto> ByMerchant { get; set; } = [];
        public List<ServiceRevenueDto> ByService { get; set; } = [];
        public List<DailyRevenueDto> Daily { get; set; } = [];
    }

    public class MerchantRevenueDto
    {
        public Guid? MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int PaidOrderCount { get; set; }
    }

    /// <summary>Theo giá dịch vụ trên đơn đã thanh toán (trước phụ phí cân và giảm giá).</summary>
    public class ServiceRevenueDto
    {
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
    }

    public class DailyRevenueDto
    {
        /// <summary>Ngày theo giờ Việt Nam, yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public class MerchantCommissionDto
    {
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public int PaidOrderCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal PayoutAmount { get; set; }
    }

    public class SettlementDto
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public int OrderCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal PayoutAmount { get; set; }
        public SettlementStatus Status { get; set; }
        public string? PaymentReference { get; set; }
        public DateTime? PaidAt { get; set; }
        public long CreatedOn { get; set; }
    }
}
