using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace WashGo.Domain.Entities.Finance
{
    /// <summary>
    /// Kỳ đối soát doanh thu của một Merchant (UC 3.8.2). Số liệu được chốt (snapshot) lúc tạo kỳ,
    /// nên sửa % hoa hồng hoặc phát sinh hoàn tiền sau đó không làm đổi kỳ đã tạo.
    /// </summary>
    [SevagoTable(WashGoDbProperties.MerchantSettlements, Schema = WashGoDbProperties.Schema)]
    public class MerchantSettlement : BaseEntity
    {
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
        public decimal PeriodPayoutAmount { get; set; }
        public decimal CarryForwardAmount { get; set; }
        public Guid? CarriedToSettlementId { get; set; }
        public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
        public string? PaymentReference { get; set; }
        public DateTime? PaidAt { get; set; }
        public Guid? CreatedByUserId { get; set; }
        [Column(TypeName = "jsonb")]
        public string? BreakdownSnapshotJson { get; set; }
    }
}
