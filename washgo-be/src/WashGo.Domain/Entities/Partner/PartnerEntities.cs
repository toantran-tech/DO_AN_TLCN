using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{
    [SevagoTable(WashGoDbProperties.Customers, Schema = WashGoDbProperties.Schema)]
    public class Customer : Entity<Guid>
    {
        public Customer() { }
        public Customer(Guid id) : base(id) { }

        public string? Address { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public int LoyaltyPoints { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.ServiceAreas, Schema = WashGoDbProperties.Schema)]
    public class ServiceArea : Entity<Guid>
    {
        public ServiceArea() { }
        public ServiceArea(Guid id) : base(id) { }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal RadiusKm { get; set; } = 2;
        public bool IsActive { get; set; } = true;
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

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

    [SevagoTable(WashGoDbProperties.MerchantServiceAreas, Schema = WashGoDbProperties.Schema)]
    public class MerchantServiceArea : Entity<Guid>
    {
        public MerchantServiceArea() { }
        public MerchantServiceArea(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }
        public Guid ServiceAreaId { get; set; }
        public int Priority { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public Guid? AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.MerchantImages, Schema = WashGoDbProperties.Schema)]
    public class MerchantImage : Entity<Guid>
    {
        public MerchantImage() { }
        public MerchantImage(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }
        public string ImageType { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
        public string? Metadata { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.Shippers, Schema = WashGoDbProperties.Schema)]
    public class Shipper : Entity<Guid>
    {
        public Shipper() { }
        public Shipper(Guid id) : base(id) { }

        public Guid UserId { get; set; }
        public Guid MerchantId { get; set; }
        public VehicleType VehicleType { get; set; } = VehicleType.Motorbike;
        public string? VehiclePlate { get; set; }
        public bool IsActive { get; set; } = true;
        public ShipperStatus Status { get; set; } = ShipperStatus.Available;
        public string? IdentityNumber { get; set; }
        public string? IdentityImages { get; set; }
        public int TotalDeliveries { get; set; }
        public decimal Rating { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}