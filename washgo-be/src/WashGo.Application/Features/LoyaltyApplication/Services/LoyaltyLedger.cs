using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Payment;

namespace WashGo.Application.Features.LoyaltyApplication.Services;

/// <summary>Called inside the payment/refund UoW, after transaction/order locks. Wallet lock is always last.</summary>
public class LoyaltyLedger(ILoyaltyWalletStore wallets, IRepository<LoyaltyEntry, Guid> entries) : ITransientDependency
{
    public async Task<bool> HoldAsync(PaymentTransaction payment, Guid customerId)
    {
        if (payment.PointsToRedeem == 0) return true;
        var wallet = await wallets.LockAsync(customerId);
        if (LoyaltyMath.Available(wallet.NetPoints, wallet.HeldPoints) < payment.PointsToRedeem) return false;
        await AppendAsync(wallet, payment, $"hold:{payment.Id}", "Hold", 0, payment.PointsToRedeem);
        payment.PointsHeld = true;
        return true;
    }

    public async Task ReleaseAsync(PaymentTransaction payment, Guid customerId)
    {
        if (!payment.PointsHeld) return;
        var wallet = await wallets.LockAsync(customerId);
        await AppendAsync(wallet, payment, $"release:{payment.Id}", "Release", 0, -payment.PointsToRedeem);
        payment.PointsHeld = false;
    }

    public async Task SettleAsync(PaymentTransaction payment, Guid customerId)
    {
        if (payment.LoyaltyVersion != 1 || payment.LoyaltyApplied) return;
        if (payment.PointsToRedeem > 0 && !payment.PointsHeld)
            throw new InvalidOperationException("Payment has no matching points reservation.");
        var wallet = await wallets.LockAsync(customerId);
        if (payment.PointsHeld)
            await AppendAsync(wallet, payment, $"redeem:{payment.Id}", "Redeem", -payment.PointsToRedeem, -payment.PointsToRedeem);
        payment.PointsHeld = false;
        payment.PointsEarned = LoyaltyMath.Earn(payment.Amount, payment.LoyaltyEarnPercent);
        await AppendAsync(wallet, payment, $"earn:{payment.Id}", "Earn", payment.PointsEarned, 0);
        payment.LoyaltyApplied = true;
    }

    public async Task RefundAsync(PaymentTransaction payment, RefundRequest refund, decimal cumulativeCashRefund)
    {
        if (!payment.LoyaltyApplied || refund.IsExcessPayment) return;
        var wallet = await wallets.LockAsync(refund.CustomerId);
        var returned = checked(payment.PointsReturned + refund.PointsAmount);
        if (returned > payment.PointsToRedeem) throw new InvalidOperationException("Cannot return more points than originally spent.");
        var reversed = LoyaltyMath.ReversedReward(payment.PointsEarned, cumulativeCashRefund, payment.Amount);
        refund.PointsAmount = checked(returned - payment.PointsReturned);
        refund.RewardPointsReversed = checked(reversed - payment.RewardPointsReversed);
        if (refund.PointsAmount < 0 || refund.RewardPointsReversed < 0)
            throw new InvalidOperationException("Refund points cumulative totals cannot decrease.");
        await AppendAsync(wallet, payment, $"refund-points:{refund.Id}", "RefundPoints", refund.PointsAmount, 0, refund.Id);
        await AppendAsync(wallet, payment, $"reverse-reward:{refund.Id}", "ReverseReward", -refund.RewardPointsReversed, 0, refund.Id);
        payment.PointsReturned = returned;
        payment.RewardPointsReversed = reversed;
    }

    private async Task AppendAsync(LoyaltyWallet wallet, PaymentTransaction payment, string key, string kind,
        long delta, long heldDelta, Guid? refundId = null)
    {
        if (await entries.AnyAsync(x => x.EventKey == key)) return;
        wallet.NetPoints = checked(wallet.NetPoints + delta);
        wallet.HeldPoints = checked(wallet.HeldPoints + heldDelta);
        if (wallet.HeldPoints < 0) throw new InvalidOperationException("Points reservation underflow.");
        await entries.InsertAsync(new LoyaltyEntry(Guid.NewGuid())
        {
            CustomerId = wallet.Id, OrderId = payment.OrderId, PaymentTransactionId = payment.Id,
            RefundRequestId = refundId, EventKey = key, Kind = kind, PointsDelta = delta,
            HeldDelta = heldDelta, BalanceAfter = wallet.NetPoints, HeldAfter = wallet.HeldPoints
        });
        await wallets.SaveAsync(wallet);
    }
}
