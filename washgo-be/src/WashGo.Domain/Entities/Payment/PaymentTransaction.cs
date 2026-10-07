using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Constants;
using WashOrderEntity = WashGo.Domain.Entities.WashOrder.WashOrder;

namespace WashGo.Domain.Entities.Payment
{
    [SevagoTable(WashGoDbProperties.PaymentTransactions, Schema = WashGoDbProperties.Schema)]
    public class PaymentTransaction : BaseEntity
    {
        public Guid OrderId { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string? ProviderTransactionId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;
        public decimal Amount { get; set; }
        public string? PaymentUrl { get; set; }
        public string? FailureReason { get; set; }
        public DateTime? CompletedAt { get; set; }

        public virtual WashOrderEntity? Order { get; set; }
    }
}
