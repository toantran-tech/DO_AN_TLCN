using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{

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
