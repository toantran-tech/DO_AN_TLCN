using System;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    public static class OverdueFeeCalculator
    {
        // Each started 24-hour period is charged at the rate for that overdue-day band.
        public static decimal Calculate(DateTime? pickupDeadline, DateTime now)
        {
            if (!pickupDeadline.HasValue || now <= pickupDeadline.Value)
                return 0m;

            var overdue = now - pickupDeadline.Value;
            var chargedDays = (long)Math.Ceiling(overdue.TotalHours / 24d);
            decimal total = 0m;
            if (chargedDays >= 1) total += 10000m;
            if (chargedDays >= 2) total += 20000m;
            if (chargedDays >= 3) total += 50000m;
            if (chargedDays > 3) total += (chargedDays - 3) * 100000m;
            return total;
        }

        // Storage fee in warehouse: 100,000 VND for each started 24-hour period after moving to storage.
        public static decimal CalculateStorageFee(DateTime? movedToStorageAt, DateTime now)
        {
            if (!movedToStorageAt.HasValue || now <= movedToStorageAt.Value)
                return 0m;

            var overdueInStorage = now - movedToStorageAt.Value;
            var chargedDays = (long)Math.Ceiling(overdueInStorage.TotalHours / 24d);
            return Math.Max(1, chargedDays) * 100000m;
        }

        // Total fee due when picking up: includes locker overdue fee + warehouse storage fee (if moved to storage).
        public static decimal GetTotalStorageDue(WashOrder order, DateTime now)
        {
            if (order.MovedToStorageAt.HasValue)
            {
                var lockerFee = order.LockerOverdueFeeAtStorageTransfer
                    ?? Calculate(order.PickupDeadline, order.MovedToStorageAt.Value);
                var warehouseFee = CalculateStorageFee(order.MovedToStorageAt, now);
                return lockerFee + warehouseFee;
            }

            return Calculate(order.PickupDeadline, now);
        }

        public static bool IsTransferToStorageDue(DateTime? pickupDeadline, DateTime now) =>
            pickupDeadline.HasValue && now - pickupDeadline.Value > TimeSpan.FromHours(72);
    }
}
