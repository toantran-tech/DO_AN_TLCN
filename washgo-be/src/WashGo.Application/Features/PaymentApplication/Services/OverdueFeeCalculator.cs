using System;
using WashGo.Domain.Entities.WashOrder;

using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    public static class OverdueFeeCalculator
    {
        // Each started 24-hour period is charged at the configured rate for that overdue-day band.
        public static decimal Calculate(DateTime? pickupDeadline, DateTime now, SystemSetting settings)
        {
            if (!pickupDeadline.HasValue || now <= pickupDeadline.Value)
                return 0m;

            var overdue = now - pickupDeadline.Value;
            var chargedDays = (long)Math.Ceiling(overdue.TotalHours / 24d);
            decimal total = 0m;
            if (chargedDays >= 1) total += settings.OverdueFeeDay1;
            if (chargedDays >= 2) total += settings.OverdueFeeDay2;
            if (chargedDays >= 3) total += settings.OverdueFeeDay3;
            if (chargedDays > 3) total += (chargedDays - 3) * settings.StorageFeePerDay;
            return total;
        }

        // Warehouse storage fee for each started 24-hour period after moving to storage.
        public static decimal CalculateStorageFee(DateTime? movedToStorageAt, DateTime now, SystemSetting settings)
        {
            if (!movedToStorageAt.HasValue || now <= movedToStorageAt.Value)
                return 0m;

            var overdueInStorage = now - movedToStorageAt.Value;
            var chargedDays = (long)Math.Ceiling(overdueInStorage.TotalHours / 24d);
            return Math.Max(1, chargedDays) * settings.StorageFeePerDay;
        }

        // Total fee due when picking up: includes locker overdue fee + warehouse storage fee (if moved to storage).
        public static decimal GetTotalStorageDue(WashOrder order, DateTime now, SystemSetting settings)
        {
            settings = OrderOperationalPolicy.Resolve(order, settings);
            if (order.MovedToStorageAt.HasValue)
            {
                var lockerFee = order.LockerOverdueFeeAtStorageTransfer
                    ?? Calculate(order.PickupDeadline, order.MovedToStorageAt.Value, settings);
                var warehouseFee = CalculateStorageFee(order.MovedToStorageAt, now, settings);
                return lockerFee + warehouseFee;
            }

            return Calculate(order.PickupDeadline, now, settings);
        }

        public static bool IsTransferToStorageDue(DateTime? pickupDeadline, DateTime now) =>
            pickupDeadline.HasValue && now - pickupDeadline.Value > TimeSpan.FromHours(SystemSetting.StorageTransferAfterHours);
    }
}
