using System;

namespace WashGo.Domain.Entities.WashOrder
{
    /// <summary>
    /// Hạn gửi đồ của một đơn: chốt lúc tạo đơn theo cài đặt "thời gian gửi đồ tối đa" (UC 3.9.1), để khách thấy
    /// đúng hạn của mình; Admin đổi SLA chỉ áp dụng cho đơn tạo sau đó. Kiểm tra gửi đồ, phí hủy và lời nhắc dùng chung mốc này.
    /// </summary>
    public static class OrderDepositRules
    {
        /// <summary>Whole seconds, so the value returned on create equals the one read back from the database.</summary>
        public static DateTime DeadlineFor(DateTime createdAt, int windowMinutes)
        {
            var deadline = createdAt.AddMinutes(windowMinutes);
            return new DateTime(deadline.Ticks - deadline.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        }

        /// <summary>Đơn cũ chưa có hạn lưu sẵn thì tính từ thời điểm tạo + cài đặt hiện hành.</summary>
        public static DateTime DeadlineOf(WashOrder order, int currentWindowMinutes) =>
            order.DepositDeadline ?? DeadlineFor(DateTimeOffset.FromUnixTimeSeconds(order.CreatedOn).UtcDateTime, currentWindowMinutes);

        public static bool IsExpired(WashOrder order, DateTime now, int currentWindowMinutes) =>
            now > DeadlineOf(order, currentWindowMinutes);
    }
}
