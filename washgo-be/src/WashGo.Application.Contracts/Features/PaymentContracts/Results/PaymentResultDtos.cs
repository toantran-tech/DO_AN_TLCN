using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results
{
    public class PaymentResultDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
    }
}
