using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ServiceProposalContracts.Results
{
    public class ServiceProposalDataDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public int EstimatedDurationHours { get; set; }
        public WashServiceType ServiceType { get; set; }
        public string Unit { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
    }

    public class ServiceProposalDto
    {
        public string Version { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public Guid? ServiceId { get; set; }
        public ServiceChangeRequestType RequestType { get; set; }
        public ServiceApprovalStatus Status { get; set; }
        public ServiceProposalDataDto Proposed { get; set; } = new();
        /// <summary>Dữ liệu dịch vụ tại thời điểm gửi đề xuất chỉnh sửa (null với đề xuất thêm mới).</summary>
        public ServiceProposalDataDto? Current { get; set; }
        public string? Reason { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
