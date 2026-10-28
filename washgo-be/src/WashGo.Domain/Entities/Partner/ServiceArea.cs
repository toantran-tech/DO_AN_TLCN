using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

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
}
