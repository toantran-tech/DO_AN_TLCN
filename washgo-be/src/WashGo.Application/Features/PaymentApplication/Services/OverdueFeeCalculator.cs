using System;

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

        public static bool IsTransferToStorageDue(DateTime? pickupDeadline, DateTime now) =>
            pickupDeadline.HasValue && now - pickupDeadline.Value > TimeSpan.FromHours(72);
    }
}
