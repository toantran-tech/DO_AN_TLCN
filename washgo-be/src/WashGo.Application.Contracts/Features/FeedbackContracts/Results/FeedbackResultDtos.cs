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
        public List<string> VideoUrls { get; set; } = [];
        public bool IsHidden { get; set; }
        public string? HiddenReason { get; set; }
        public DateTime? HiddenAt { get; set; }
        public long CreatedOn { get; set; }
    }

    public class IncidentResultDto
    {
        public string? CustomerAvatarUrl { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhoneNumber { get; set; }
        public List<string> MerchantImageUrls { get; set; } = [];
        public string? MerchantEmail { get; set; }
        public string? MerchantPhone { get; set; }
        public string ContactBasis => "Current profile, Admin only; not part of incident version.";
        public Guid? ReviewedBy { get; set; }
        public bool CanResolve { get; set; }
        public bool CanReject { get; set; }
        public bool CanConfirmCompensation { get; set; }
        public string Version { get; set; } = string.Empty;
        public int? CompensationPercentApplied { get; set; }
        public string? CompensationSettingsVersion { get; set; }
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
        public List<string> VideoUrls { get; set; } = [];
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
        public string? ClientRequestId { get; set; }
        public DateTime? CustomerUpdatedAt { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public string? WithdrawReason { get; set; }
        /// <summary>Khách còn sửa/rút/bổ sung được (đang chờ xử lý).</summary>
        public bool CanEdit => Status == IncidentStatus.Pending;
    }

    /// <summary>Điều kiện báo cáo sự cố của một đơn (UC 1.4.2).</summary>
    public class IncidentEligibilityDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public bool CanReport { get; set; }
        /// <summary>Lý do không báo cáo được; null khi được phép.</summary>
        public string? Reason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ReportDeadline { get; set; }
        public Guid? PendingIncidentId { get; set; }
        public List<Guid> IncidentIds { get; set; } = [];
    }

    public class IncidentMessageDto
    {
        public Guid Id { get; set; }
        public Guid IncidentId { get; set; }
        public string SenderRole { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }
}
