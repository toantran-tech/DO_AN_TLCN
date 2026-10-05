using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Core.Domain.Shared.Enums;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    [SevagoTable(WashGoDbProperties.Orders, Schema = WashGoDbProperties.Schema)]
    public class WashOrder : BaseEntity, IHasAuditLog
    {
        public WashOrder()
        {
            Items = new List<WashOrderItem>();
            Timeline = new List<WashOrderTimeline>();
        }

        [Filterable(ColumnVariant.Text)]
        public string OrderCode { get; set; } = string.Empty;
        public string? MerchantOrderCode { get; set; }
        public Guid CustomerId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string CustomerName { get; set; } = string.Empty;

        [Filterable(ColumnVariant.Text)]
        public string CustomerPhoneNumber { get; set; } = string.Empty;

        public Guid LockerId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string LockerName { get; set; } = string.Empty;

        public Guid BoxId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public Guid? ReturnLockerId { get; set; }
        public Guid? ReturnBoxId { get; set; }
        public string? ReturnBoxNumber { get; set; }
        public Guid? MerchantId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string? MerchantName { get; set; }

        public Guid? ShipperId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string? ShipperName { get; set; }

        public Guid? PromotionId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal? ActualWeightKg { get; set; }
        public int? ActualItemCount { get; set; }

        [Filterable(ColumnVariant.MultiSelect)]
        public WashOrderStatus Status { get; set; } = WashOrderStatus.Pending;

        [Filterable(ColumnVariant.MultiSelect)]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string DepositPinCode { get; set; } = string.Empty;
        public string PickupPinCode { get; set; } = string.Empty;
        public string QrCodeString { get; set; } = string.Empty;

        [Filterable(ColumnVariant.DateRange, UnixUnit = UnixTimestampUnit.Seconds)]
        public long ExpectedDeliveryTime { get; set; }

        public DateTime? EstimatedFinishAt { get; set; }
        public DateTime? EtaUpdatedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime? DepositDeadline { get; set; }
        public DateTime? ReturnDeadline { get; set; }
        public DateTime? PickupDeadline { get; set; }
        public DateTime? PaymentDeadline { get; set; }
        public decimal OverdueFeeTotal { get; set; }
        public decimal StorageFeeTotal { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public OrderActorType? CancelledByType { get; set; }
        public string? CancellationReason { get; set; }
        public decimal CancelFeeAmount { get; set; }
        public string? CustomerNote { get; set; }

        public virtual ICollection<WashOrderItem> Items { get; set; }
        public virtual ICollection<WashOrderTimeline> Timeline { get; set; }
    }
}