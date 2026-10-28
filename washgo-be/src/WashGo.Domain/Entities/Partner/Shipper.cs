using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

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
