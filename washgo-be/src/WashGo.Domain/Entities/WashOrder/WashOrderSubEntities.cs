using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    [SevagoTable(WashGoDbProperties.OrderItems, Schema = WashGoDbProperties.Schema)]
    public class WashOrderItem : Entity<Guid>
    {
        public Guid WashOrderId { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [SevagoTable(WashGoDbProperties.OrderStatusHistory, Schema = WashGoDbProperties.Schema)]
    public class WashOrderTimeline : Entity<Guid>
    {
        public Guid WashOrderId { get; set; }
        public WashOrderStatus? FromStatus { get; set; }
        public WashOrderStatus Status { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public long Timestamp { get; set; }
        public string? Note { get; set; }
    }
}