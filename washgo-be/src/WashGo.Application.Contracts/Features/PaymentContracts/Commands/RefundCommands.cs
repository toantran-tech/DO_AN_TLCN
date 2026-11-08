using System;
using WashGo.Domain.Shared.Enums;
using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Commands
{
    public class CreateRefundRequestCommand
    {
        public Guid? OperationId { get; set; }
        /// <summary>null = phần tiền gốc còn được hoàn; số dương hỗ trợ yêu cầu hoàn một phần.</summary>
        public decimal? Amount { get; set; }
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ReviewRefundRequestCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        [JsonRequired]
        public bool Approve { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ConfirmRefundCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string RefundReference { get; set; } = string.Empty;
    }

    public class CreateOverdueFeePaymentCommand
    {
        public Guid OrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.VnPay;
        [JsonIgnore]
        public string ClientIpAddress { get; set; } = "127.0.0.1";
    }

}
