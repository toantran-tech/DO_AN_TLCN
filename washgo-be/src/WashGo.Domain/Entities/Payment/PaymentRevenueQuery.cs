using System;
using System.Linq;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Payment;

public sealed class PaymentRevenueAllocation
{
    public Guid TransactionId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? SettlementId { get; init; }
    public Guid? MerchantIdSnapshot { get; init; }
    public decimal? CommissionRateSnapshot { get; init; }
    public string? FinancialSnapshotBasis { get; init; }
    public DateTime? CompletedAt { get; init; }
    public PaymentPurpose Purpose { get; init; }
    public decimal Amount { get; init; }
    public long Points { get; init; }
}

public static class PaymentRevenueQuery
{
    public static IQueryable<PaymentTransaction> WithLaundry(this IQueryable<PaymentTransaction> source) =>
        source.Where(p => p.Purpose == PaymentPurpose.Order || p.Purpose == PaymentPurpose.Combined && p.LaundryAmount > 0m);

    public static IQueryable<PaymentRevenueAllocation> RevenueAllocations(this IQueryable<PaymentTransaction> source)
    {
        var receipts = source.Where(p => !p.IsDeleted && !p.RequiresReconciliation &&
            (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded));
        var laundry = receipts.WithLaundry().Select(p => new PaymentRevenueAllocation
        {
            TransactionId = p.Id, OrderId = p.OrderId, SettlementId = p.SettlementId, MerchantIdSnapshot = p.MerchantIdSnapshot,
            CommissionRateSnapshot = p.CommissionRateSnapshot, FinancialSnapshotBasis = p.FinancialSnapshotBasis,
            CompletedAt = p.CompletedAt, Purpose = PaymentPurpose.Order,
            Amount = p.Purpose == PaymentPurpose.Order ? p.Amount : p.LaundryAmount - p.PointsToRedeem,
            Points = p.LoyaltyApplied ? p.PointsToRedeem : 0
        });
        var storage = receipts.Where(p => p.Purpose == PaymentPurpose.OverdueStorageFee ||
            p.Purpose == PaymentPurpose.Combined && p.StorageAmount > 0m).Select(p => new PaymentRevenueAllocation
        {
            TransactionId = p.Id, OrderId = p.OrderId, SettlementId = p.SettlementId, MerchantIdSnapshot = p.MerchantIdSnapshot,
            CommissionRateSnapshot = p.CommissionRateSnapshot, FinancialSnapshotBasis = p.FinancialSnapshotBasis,
            CompletedAt = p.CompletedAt, Purpose = PaymentPurpose.OverdueStorageFee,
            Amount = p.Purpose == PaymentPurpose.OverdueStorageFee ? p.Amount : p.StorageAmount, Points = 0
        });
        var cancellation = receipts.Where(p => p.Purpose == PaymentPurpose.CancellationFee ||
            p.Purpose == PaymentPurpose.Combined && p.CancellationAmount > 0m).Select(p => new PaymentRevenueAllocation
        {
            TransactionId = p.Id, OrderId = p.OrderId, SettlementId = p.SettlementId, MerchantIdSnapshot = p.MerchantIdSnapshot,
            CommissionRateSnapshot = p.CommissionRateSnapshot, FinancialSnapshotBasis = p.FinancialSnapshotBasis,
            CompletedAt = p.CompletedAt, Purpose = PaymentPurpose.CancellationFee,
            Amount = p.Purpose == PaymentPurpose.CancellationFee ? p.Amount : p.CancellationAmount, Points = 0
        });
        return laundry.Concat(storage).Concat(cancellation);
    }
}
