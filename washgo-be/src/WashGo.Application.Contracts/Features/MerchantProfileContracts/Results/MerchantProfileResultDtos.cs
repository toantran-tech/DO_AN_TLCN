using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts.Results
{
    public class MerchantProfileDataDto
    {
        public bool DocumentsSnapshotIncluded { get; set; }
        public Guid? BusinessLicenseDocumentId { get; set; }
        /// <summary>Phiên bản hồ sơ lúc gửi yêu cầu; null cho yêu cầu legacy.</summary>
        public string? BaseMerchantVersion { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        /// <summary>null trong yêu cầu cũ = không đề cập (giữ nguyên); chuỗi rỗng = đề nghị xóa.</summary>
        public string? TaxCode { get; set; }
        public string? BusinessLicense { get; set; }
    }

    public class MerchantProfileRequestDto
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public ServiceApprovalStatus Status { get; set; }
        public MerchantProfileDataDto Proposed { get; set; } = new();
        public MerchantProfileDataDto Current { get; set; } = new();
        public List<string> ChangedFields { get; set; } = [];
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class MerchantProfileDto
    {
        public List<WashGo.Application.Contracts.Features.FileContracts.Results.PrivateDocumentDto> Documents { get; set; } = [];
        public Guid? BusinessLicenseDocumentId { get; set; }
        public Guid? ContractDocumentId { get; set; }
        public Guid? SlaDocumentId { get; set; }
        public Guid Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        /// <summary>Giấy phép kinh doanh đã duyệt (số giấy phép hoặc URL ảnh); null = chưa khai báo.</summary>
        public string? BusinessLicense { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public MerchantStatus Status { get; set; }
        public decimal Rating { get; set; }
        public int TotalOrders { get; set; }
        public decimal CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; }
        public int MaxCapacity { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public DateTime? JoinedAt { get; set; }
        public int PublicRatingCount { get; set; }
        public MerchantProfileRequestDto? PendingRequest { get; set; }
    }
}
