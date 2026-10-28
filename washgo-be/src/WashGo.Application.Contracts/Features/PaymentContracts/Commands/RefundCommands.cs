using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Commands
{
    public class CreateRefundRequestCommand
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ReviewRefundRequestCommand
    {
        public bool Approve { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ConfirmRefundCommand
    {
        public string RefundReference { get; set; } = string.Empty;
    }

    public class CreateOverdueFeePaymentCommand
    {
        public Guid OrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.VnPay;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ClientIpAddress { get; set; } = "127.0.0.1";
    }

}
