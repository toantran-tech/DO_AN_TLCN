using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Contracts.Features.FileContracts.Results;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results
{
    public class MerchantResultDto
    {
        public Guid? BusinessLicenseDocumentId { get; set; }
        public Guid? ContractDocumentId { get; set; }
        public Guid? SlaDocumentId { get; set; }
        public List<PrivateDocumentDto> Documents { get; set; } = [];
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        /// <summary>Sent = SMTP đã chấp nhận, không bảo đảm đã vào inbox. Unknown có thể đang gửi hoặc chưa xác định.</summary>
        public CredentialDeliveryStatus CredentialDelivery { get; set; }
        public bool CredentialEmailSent => CredentialDelivery == CredentialDeliveryStatus.Sent;
        public DateTime? CredentialSentAt { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? BusinessLicense { get; set; }
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public MerchantStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        /// <summary>Điểm trung bình đánh giá công khai (1 chữ số thập phân); null khi chưa có đánh giá công khai (không phải 5 sao).</summary>
        public decimal? Rating { get; set; }
        public bool HasRating => PublicRatingCount > 0;
        public int PublicRatingCount { get; set; }
        public string TotalRevenueBasis => "Sum of FinalAmount of orders currently Paid; face value, not net receipts, profit or Merchant payout.";
        public string ContractDateBasis => "UTC timestamp; display calendar dates in Asia/Ho_Chi_Minh.";
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; }
        public int CurrentWorkload { get; set; }
        public int MaxCapacity { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string? SlaDocUrl { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        /// <summary>Số đơn chưa xử lý xong (từ Chờ gửi đồ đến Sẵn sàng trả).</summary>
        public int UnfinishedOrders { get; set; }
        public long CreatedOn { get; set; }
        public List<Guid> ServiceAreaIds { get; set; } = [];
    }
}

