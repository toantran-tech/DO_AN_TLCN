using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Commands
{
    public class CreateRatingCommand
    {
        public Guid OrderId { get; set; }
        public int? OverallStars { get; set; }
        public int? QualityStars { get; set; }
        public int? SpeedStars { get; set; }
        public int? AttitudeStars { get; set; }
        public int? PackagingStars { get; set; }
        public string? Comment { get; set; }
        public List<string> ImageUrls { get; set; } = [];
    }

    public class SetRatingVisibilityCommand
    {
        public bool IsHidden { get; set; }
        public string? Reason { get; set; }
    }

    public class CreateIncidentCommand
    {
        public Guid OrderId { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
    }

    public class ResolveIncidentCommand
    {
        public string ResolutionNote { get; set; } = string.Empty;
        public ResponsibleParty? ResponsibleParty { get; set; }
        public CompensationLevel CompensationLevel { get; set; }
        /// <summary>Giá trị đồ bị ảnh hưởng; tiền bồi thường = giá trị x % theo mức.</summary>
        public decimal ItemValue { get; set; }
    }

    public class RejectIncidentCommand
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class ConfirmCompensationCommand
    {
        public string Reference { get; set; } = string.Empty;
    }
}
