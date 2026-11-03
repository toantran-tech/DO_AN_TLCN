using System;

namespace WashGo.Application.Contracts.Features.OrderAssignmentContracts.Queries
{
    /// <summary>UC 3.3.1: lọc đơn chờ phân công theo khu vực và thời gian gửi đồ (Unix giây).</summary>
    public class WaitingOrderFilterQuery
    {
        public string? District { get; set; }
        public string? City { get; set; }
        public Guid? ServiceAreaId { get; set; }
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        public bool? OverdueOnly { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
