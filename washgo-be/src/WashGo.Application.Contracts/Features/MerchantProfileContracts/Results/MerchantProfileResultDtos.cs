using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts.Results
{
    public class MerchantProfileDataDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string> ImageUrls { get; set; } = [];
    }

    public class MerchantProfileRequestDto
    {
        public Guid Id { get; set; }
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
        public Guid Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
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
