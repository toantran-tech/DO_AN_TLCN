using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Results
{
    public class RatingResultDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public int OverallStars { get; set; }
        public int? QualityStars { get; set; }
        public int? SpeedStars { get; set; }
        public int? AttitudeStars { get; set; }
        public int? PackagingStars { get; set; }
        public string? Comment { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public bool IsHidden { get; set; }
        public string? HiddenReason { get; set; }
        public DateTime? HiddenAt { get; set; }
        public long CreatedOn { get; set; }
    }

    public class IncidentResultDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public IncidentStatus Status { get; set; }
        public string? ResolutionNote { get; set; }
        public ResponsibleParty? ResponsibleParty { get; set; }
        public CompensationLevel CompensationLevel { get; set; }
        public decimal ItemValue { get; set; }
        public decimal CompensationAmount { get; set; }
        public CompensationStatus CompensationStatus { get; set; }
        public string? CompensationReference { get; set; }
        public DateTime? CompensationPaidAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public long CreatedOn { get; set; }
    }
}
