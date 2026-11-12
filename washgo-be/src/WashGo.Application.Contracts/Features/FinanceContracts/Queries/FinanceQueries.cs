using WashGo.Core.Domain.Shared.Bases;
using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Queries
{
    /// <summary>Khoảng thời gian Unix giây; bỏ trống thì mặc định 30 ngày gần nhất.</summary>
    public class FinancePeriodQuery
    {
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        public Guid? MerchantId { get; set; }
        /// <summary>Unix giây: chỉ tính giao dịch đến thời điểm này (truyền generatedAt của báo cáo đang xem khi xuất CSV để khớp dữ liệu).</summary>
        public long? AsOf { get; set; }
        /// <summary>"day" (mặc định) hoặc "hour" (chỉ khi kỳ ≤ 48 giờ): bucket theo giờ Việt Nam từ giao dịch thật.</summary>
        public string? Granularity { get; set; }
        /// <summary>Chỉ dùng cho /finance/commissions/paged: page từ 1, take 1-100 (mặc định 20).</summary>
        public int? Page { get; set; }
        public int? Take { get; set; }
    }

    public class SettlementFilterQuery : BaseFilterQuery
    {
        public Guid? MerchantId { get; set; }
        public SettlementStatus? Status { get; set; }
        public SettlementFilterQuery() { Page = 1; Take = 20; }
        public long PaidFromDate { get; set; }
        public long PaidToDate { get; set; }
    }
}
