using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;
using WashOrderEntity = WashGo.Domain.Entities.WashOrder.WashOrder;

namespace WashGo.Domain.Entities.Payment
{
    [SevagoTable(WashGoDbProperties.RefundRequests, Schema = WashGoDbProperties.Schema)]
    public class RefundRequest : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public RefundRequestStatus Status { get; set; } = RefundRequestStatus.Pending;
        public string? ReviewNote { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RefundReference { get; set; }
        public DateTime? RefundedAt { get; set; }
        public virtual WashOrderEntity? Order { get; set; }
    }
}
