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
}
