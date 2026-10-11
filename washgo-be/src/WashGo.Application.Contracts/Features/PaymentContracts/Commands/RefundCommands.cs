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

    public class RefundRequestDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public RefundRequestStatus Status { get; set; }
        public string? ReviewNote { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RefundReference { get; set; }
        public DateTime? RefundedAt { get; set; }
        public long CreatedOn { get; set; }
    }
}
