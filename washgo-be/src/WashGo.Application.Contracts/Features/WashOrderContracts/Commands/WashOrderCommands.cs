using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Commands
{
    public class CreateWashOrderCommand
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public Guid LockerId { get; set; }
        public Guid BoxId { get; set; }
        public Guid? ReturnLockerId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string? CustomerNote { get; set; }
        public List<CreateWashOrderItemDto> Items { get; set; } = [];
    }

    public class CreateWashOrderItemDto
    {
        public Guid ServiceId { get; set; }
        public int Quantity { get; set; }
        public decimal? WeightKg { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateWashOrderStatusCommand
    {
        public Guid OrderId { get; set; }
        public WashOrderStatus NewStatus { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public string? QrCodeScanned { get; set; }
        public string? PinCodeEntered { get; set; }
        public string? Note { get; set; }
    }

    public class AssignShipperCommand
    {
        public Guid OrderId { get; set; }
        public Guid ShipperId { get; set; }
        public string ShipperName { get; set; } = string.Empty;
    }
}
