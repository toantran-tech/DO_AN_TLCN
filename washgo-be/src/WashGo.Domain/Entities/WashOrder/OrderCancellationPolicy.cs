using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    public static class OrderCancellationPolicy
    {
        public static bool TryCalculate(WashOrder order, DateTime now,
            out decimal basis, out int percent, out decimal fee)
        {
            basis = order.TotalAmount;
            percent = 0;
            fee = 0;
            if (order.PaymentStatus == PaymentStatus.Refunded) return false;
            switch (order.Status)
            {
                case WashOrderStatus.Pending:
                    percent = new DateTimeOffset(now).ToUnixTimeSeconds() - order.CreatedOn > 1800 ? 30 : 0;
                    break;
                case WashOrderStatus.Deposited:
                    percent = 30;
                    break;
                case WashOrderStatus.Collected when order.ActualWeightKg > 0:
                    basis = order.FinalAmount;
                    percent = 50;
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
