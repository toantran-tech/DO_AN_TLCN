using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>UC 4.1.3: hồ sơ cá nhân của Shipper.</summary>
    public class ShipperProfileDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? AvatarUrl { get; set; }
        public VehicleType VehicleType { get; set; }
        public string? VehiclePlate { get; set; }
        public ShipperStatus Status { get; set; }
        public bool IsActive { get; set; }
        public int TotalDeliveries { get; set; }
        public decimal Rating { get; set; }
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> MerchantImageUrls { get; set; } = [];
        public string? MerchantPhone { get; set; }
        public string? MerchantEmail { get; set; }
        public string? MerchantAddress { get; set; }
        public int? RatingCount { get; set; }
        public string RatingSource { get; set; } = "NotAvailable";
    }
}
