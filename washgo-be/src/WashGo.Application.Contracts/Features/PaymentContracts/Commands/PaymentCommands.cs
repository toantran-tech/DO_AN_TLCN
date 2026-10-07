using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Commands
{
    public class CreatePaymentCommand
    {
        public Guid OrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
    }

    public class PaymentCallbackCommand
    {
        public Guid OrderId { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}
