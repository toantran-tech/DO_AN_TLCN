using WashGo.Domain.Entities.Loyalty;

static void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
}

Check(LoyaltyMath.Earn(170000m, 1m) == 1700, "Earn only gateway cash after voucher and points.");
Check(LoyaltyMath.Earn(199.99m, 1m) == 1, "Fractional rewards round down.");
Check(LoyaltyMath.MaximumUse(180000m, 20m, 50000) == 36000, "Redemption cap after voucher.");
Check(LoyaltyMath.MaximumUse(180000m, 20m, 123) == 123, "Wallet balance caps redemption.");
Check(LoyaltyMath.Available(-500, 0) == 0 && LoyaltyMath.Available(500, 600) == 0, "Debt or reservations cannot create spendable points.");
var partial = LoyaltyMath.CancellationRefund(144000m, 36000, 90000m);
Check(partial.Cash == 72000m && partial.Points == 18000, "Fee cancellation proportionally refunds cash and points.");
foreach (var fee in new[] { 0m, 0.01m, 1m, 29.99m, 30m, 50m, 99.99m, 100m, 101m })
{
    var split = LoyaltyMath.CancellationRefund(81m, 19, fee);
    Check(split.Cash >= 0 && split.Cash <= 81 && split.Points >= 0 && split.Points <= 19, "Never mint cash or points at rounding boundaries.");
    Check(split.Cash + split.Points == Math.Max(0, 100m - fee), "Cash plus points equals remaining refundable value.");
}
Check(LoyaltyMath.ReversedReward(1700, 170000, 170000) == 1700, "Full refund reverses full reward.");
Check(LoyaltyMath.ReturnedPoints(36000, 144000, 144000) == 36000, "Full cash refund returns full originally spent points.");
var previous = 0L;
var total = 0L;
foreach (var cumulative in new[] { 1m, 17m, 50m, 99m, 100m })
{
    var target = LoyaltyMath.ReversedReward(19, cumulative, 100m);
    total += target - previous;
    previous = target;
}
Check(total == 19, "Cumulative partial refunds do not lose reward reversal to rounding.");
Console.WriteLine("PASS: loyalty cash basis, rounding, cap, reserved/debt balance, proportional cancellation and cumulative refund math.");
