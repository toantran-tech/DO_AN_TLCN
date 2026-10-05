using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    public class WashOrderResultDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public Guid LockerId { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public Guid BoxId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public string? ReturnBoxNumber { get; set; }
        public Guid? ShipperId { get; set; }
        public string? ShipperName { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public WashOrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string DepositPinCode { get; set; } = string.Empty;
        public string PickupPinCode { get; set; } = string.Empty;
        public string QrCodeString { get; set; } = string.Empty;
        public long ExpectedDeliveryTime { get; set; }
        public long CreatedOn { get; set; }
    }

    public class WashOrderDetailDto : WashOrderResultDto
    {
        public string? CustomerNote { get; set; }
        public List<WashOrderItemDto> Items { get; set; } = [];
        public List<WashOrderTimelineDto> Timeline { get; set; } = [];
    }

    public class WashOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
    }

    public class WashOrderTimelineDto
    {
        public Guid Id { get; set; }
        public WashOrderStatus Status { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public long Timestamp { get; set; }
        public string? Note { get; set; }
    }
}
