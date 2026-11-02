using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

    [SevagoTable(WashGoDbProperties.Merchants, Schema = WashGoDbProperties.Schema)]
    public class Merchant : Entity<Guid>
    {
        public Merchant() { }
        public Merchant(Guid id) : base(id) { }

        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? BusinessLicense { get; set; }
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public MerchantStatus Status { get; set; } = MerchantStatus.Active;
        public string? RejectionReason { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public decimal Rating { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal DepositAmount { get; set; }
        public DateTime? DepositUpdatedAt { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string? SlaDocUrl { get; set; }
        public decimal CommissionRate { get; set; } = 20;
        public int ServiceRadiusKm { get; set; } = 2;
        public int CurrentWorkload { get; set; }
        public int MaxCapacity { get; set; } = 50;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
