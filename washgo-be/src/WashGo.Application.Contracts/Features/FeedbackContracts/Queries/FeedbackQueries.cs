using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Queries
{
    /// <summary>FromDate/ToDate (Unix giây) lọc theo ngày đánh giá.</summary>
    public class RatingFilterQuery : BaseFilterQuery
    {
        public int? Stars { get; set; }
        public Guid? MerchantId { get; set; }
        public bool? IsHidden { get; set; }
    }

    public class IncidentFilterQuery : BaseFilterQuery
    {
        public CompensationStatus? CompensationStatus { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? CustomerId { get; set; }
        public IncidentStatus? Status { get; set; }
        public IncidentSeverity? Severity { get; set; }
        public IncidentType? Type { get; set; }
        public Guid? MerchantId { get; set; }
    }
}
