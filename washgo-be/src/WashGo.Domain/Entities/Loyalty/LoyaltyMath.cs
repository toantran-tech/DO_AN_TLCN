namespace WashGo.Domain.Entities.Loyalty;

public static class LoyaltyMath
{
    public static long Available(long net, long held) => Math.Max(0, checked(net - held));
    public static long Earn(decimal cash, decimal percent) => checked((long)decimal.Floor(cash * percent / 100m));
    public static long MaximumUse(decimal laundryAmount, decimal percent, long available) =>
        Math.Min(available, checked((long)decimal.Floor(laundryAmount * percent / 100m)));

    // Cumulative targets prevent rounding drift across several partial refunds.
    public static long ReversedReward(long earned, decimal refundedCash, decimal originalCash) =>
        originalCash <= 0 ? 0 : Math.Min(earned, checked((long)decimal.Floor(earned * refundedCash / originalCash)));
    public static long ReturnedPoints(long spent, decimal refundedValue, decimal originalValue) =>
        originalValue <= 0 ? 0 : Math.Min(spent, checked((long)decimal.Floor(spent * refundedValue / originalValue)));

    public static (decimal Cash, long Points) CancellationRefund(decimal cash, long points, decimal fee)
    {
        var total = cash + points;
        var refundable = Math.Max(0, total - fee);
        var pointRefund = Math.Max(ReturnedPoints(points, refundable, total),
            checked((long)decimal.Ceiling(Math.Max(0, refundable - cash))));
        return (Math.Max(0, refundable - pointRefund), pointRefund);
    }
}
