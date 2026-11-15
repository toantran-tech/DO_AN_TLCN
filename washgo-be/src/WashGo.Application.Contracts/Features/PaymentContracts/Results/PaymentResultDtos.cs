using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results
{
    public class PaymentResultDto
    {
        public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Order;
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal LaundryAmount { get; set; }
        public long PointsToRedeem { get; set; }
        public long PointsEarned { get; set; }
        public bool PointsHeld { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
        public PaymentTransactionStatus TransactionStatus { get; set; }
        public PaymentSessionState SessionState { get; set; }
        public bool RequiresReconciliation { get; set; }
    }
}
