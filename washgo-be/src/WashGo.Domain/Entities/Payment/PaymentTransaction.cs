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
        public Guid? SettlementId { get; set; }
        public Guid? MerchantIdSnapshot { get; set; }
        public decimal? CommissionRateSnapshot { get; set; }
        public string? FinancialSnapshotBasis { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string? ProviderTransactionId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Order;
        public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;
        public decimal Amount { get; set; }
        /// <summary>Zero for legacy receipts; only version 1 earns/redeems points.</summary>
        public int LoyaltyVersion { get; set; }
        public long PointsToRedeem { get; set; }
        public bool PointsHeld { get; set; }
        public bool LoyaltyApplied { get; set; }
        public long PointsEarned { get; set; }
        public long PointsReturned { get; set; }
        public long RewardPointsReversed { get; set; }
        public decimal LoyaltyEarnPercent { get; set; }
        public decimal LoyaltyMaxUsePercent { get; set; }
        public decimal LaundryAmount { get; set; }
        public string? PaymentUrl { get; set; }
        public string? FailureReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public PaymentSessionState SessionState { get; set; }
        public bool IsActiveSession { get; set; }
        public DateTime? GatewayVerifiedAt { get; set; }
        /// <summary>Permanent classification: never include unapplied receipts in Merchant revenue, even after refund.</summary>
        public bool RequiresReconciliation { get; set; }
        public string? ReconciliationReason { get; set; }
        public DateTime? ReconciledAt { get; set; }
        public DateTime? GatewayCreatedAt { get; set; }
        public DateTime? GatewayExpiresAt { get; set; }
        public DateTime? VerifiedClosedAt { get; set; }

        public virtual WashOrderEntity? Order { get; set; }
    }
}
