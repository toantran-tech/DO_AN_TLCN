using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Queries
{
    /// <summary>Customer lọc khiếu nại của mình.</summary>
    public class CustomerIncidentFilterQuery
    {
        public List<IncidentStatus>? Statuses { get; set; }
        public List<IncidentType>? Types { get; set; }
        /// <summary>Tìm theo mã đơn.</summary>
        public string? Keyword { get; set; }
        /// <summary>Unix giây (UTC) theo ngày gửi khiếu nại.</summary>
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        /// <summary>newest (mặc định), oldest, status (chờ xử lý trước).</summary>
        public string? Sort { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
