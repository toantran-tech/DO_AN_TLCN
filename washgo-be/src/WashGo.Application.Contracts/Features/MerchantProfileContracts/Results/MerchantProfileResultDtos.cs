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

    /// <summary>Giá trị giấy phép cũ (trường chuỗi trước khi có tài liệu riêng tư), đã phân loại để FE không phải đoán.</summary>
    public class LegacyBusinessLicenseDto
    {
        /// <summary>Number (số giấy phép), Url (đường dẫn/ảnh cũ, chỉ hiển thị dạng văn bản, không tải được), Text (văn bản khác).</summary>
        public string Kind { get; set; } = "Text";
        public string Value { get; set; } = string.Empty;
        /// <summary>true khi hồ sơ đã có tài liệu giấy phép mới (businessLicenseDocumentId); giá trị cũ chỉ để tham khảo.</summary>
        public bool SupersededByDocument { get; set; }
    }

    public class MerchantProfileDto
    {
        /// <summary>Phiên bản hồ sơ (chuỗi opaque); gửi lại trong expectedMerchantVersion khi tạo yêu cầu cập nhật.</summary>
        public string Version { get; set; } = string.Empty;
        /// <summary>null khi không có giá trị cũ.</summary>
        public LegacyBusinessLicenseDto? LegacyBusinessLicense { get; set; }
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
        /// <summary>Điểm trung bình đánh giá công khai (1 chữ số thập phân); null khi chưa có đánh giá công khai (không phải 5 sao).</summary>
        public decimal? Rating { get; set; }
        public bool HasRating => PublicRatingCount > 0;
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
