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
