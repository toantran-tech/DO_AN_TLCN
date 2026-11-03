using System;
using System.Text.Json.Serialization;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Commands
{
    public class CreatePaymentCommand
    {
        public Guid OrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        [JsonIgnore]
        public string ClientIpAddress { get; set; } = "127.0.0.1";
    }

    public class PaymentCallbackCommand
    {
        public Guid OrderId { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public bool Success { get; set; }
    }

    /// <summary>IPN body MoMo posts to /payment/momo-ipn (field names as MoMo sends them).</summary>
    public class MomoIpnCommand
    {
        public string PartnerCode { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public long TransId { get; set; }
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PayType { get; set; } = string.Empty;
        public long ResponseTime { get; set; }
        public string ExtraData { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
