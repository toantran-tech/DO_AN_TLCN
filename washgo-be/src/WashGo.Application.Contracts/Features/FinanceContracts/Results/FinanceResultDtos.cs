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
        /// <summary>Số đơn có thu/hoàn trong kỳ nhưng chưa có tỷ lệ hoa hồng chốt (cần đối chiếu; không tính hoa hồng).</summary>
        public int OrdersWithoutCommissionSnapshot { get; set; }
        /// <summary>Doanh thu thuần của các đơn trên (chưa xác định hoa hồng).</summary>
        public decimal UncoveredNetAmount { get; set; }
        /// <summary>true khi hoa hồng/thực nhận tính đủ cho toàn bộ kỳ.</summary>
        public bool CommissionComplete => OrdersWithoutCommissionSnapshot == 0;
    }

    /// <summary>Chi tiết một kỳ đối soát theo đơn: thu, hoàn, tỷ lệ chốt, hoa hồng, thực nhận.</summary>
    public class SettlementOrderLineDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal Collected { get; set; }
        public decimal Refunded { get; set; }
        public decimal Net => Collected - Refunded;
        /// <summary>Tỷ lệ chốt trên đơn; null = chưa có snapshot (cần đối chiếu).</summary>
        public decimal? CommissionRate { get; set; }
        public decimal? Commission { get; set; }
        public decimal? Payout { get; set; }
    }

    public class SettlementBreakdownDto
    {
        public Guid SettlementId { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public SettlementStatus Status { get; set; }
        /// <summary>Số đã chốt khi tạo kỳ (không đổi theo cấu hình sau này).</summary>
        public decimal SettledCommissionAmount { get; set; }
        public decimal SettledPayoutAmount { get; set; }
        public int OrdersWithoutCommissionSnapshot { get; set; }
        public List<SettlementOrderLineDto> Lines { get; set; } = [];
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
