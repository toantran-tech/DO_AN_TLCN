using System;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    public static class OrderCancellationPolicy
    {
        public static bool TryCalculate(WashOrder order, DateTime now, SystemSetting settings,
            out decimal basis, out int percent, out decimal fee)
        {
            // Spec UC 1.2.7: Pending/Deposited fees use TotalAmount (pre-discount); Collected uses FinalAmount.
            basis = order.TotalAmount;
            percent = 0;
            fee = 0;
            if (order.PaymentStatus == PaymentStatus.Refunded) return false;
            switch (order.Status)
            {
                case WashOrderStatus.Pending:
                    // Free while the box is still reserved (same deadline as the deposit check).
                    percent = OrderDepositRules.IsExpired(order, now, settings.DepositWindowMinutes)
                        ? settings.CancelLatePendingPercent : 0;
                    break;
                case WashOrderStatus.Deposited:
                    percent = settings.CancelDepositedPercent;
                    break;
                case WashOrderStatus.Collected when order.ActualWeightKg > 0:
                    basis = order.FinalAmount;
                    percent = settings.CancelCollectedPercent;
                    break;
                default:
                    return false;
            }
            if (basis < 0) return false;
            fee = decimal.Round(basis * percent / 100m, 2, MidpointRounding.AwayFromZero);
            return true;
        }

        public static bool FeeSettled(WashOrder order) =>
            order.CancelFeePaidAmount >= order.CancelFeeAmount;

        public static bool IsLockerPickup(WashOrder order) => order.Status == WashOrderStatus.Cancelled &&
            order.CancelledGoodsStatus is CancelledGoodsStatus.AtDepositLocker or CancelledGoodsStatus.AtReturnLocker;
    }
}
