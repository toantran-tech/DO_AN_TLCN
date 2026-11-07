using System;

namespace WashGo.Core.Utils
{
    /// <summary>
    /// Tiện ích xử lý múi giờ Việt Nam (UTC+7) và tính toán khoảng thời gian (Hôm nay, Tuần, Tháng, Tùy chọn).
    /// </summary>
    public static class DateTimePeriodHelper
    {
        public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static DateTime NowVietnam => DateTime.UtcNow + VietnamOffset;

        public static DateTime ToVietnamTime(DateTime utcTime) => utcTime + VietnamOffset;

        public static DateTime ToUtcTime(DateTime vietnamTime) => DateTime.SpecifyKind(vietnamTime - VietnamOffset, DateTimeKind.Utc);

        /// <summary>
        /// Tính toán khoảng thời gian hiện tại và kỳ trước tương đương (phục vụ đối soát, so sánh tăng trưởng).
        /// </summary>
        public static (string Period, DateTime? FromUtc, DateTime? ToUtc, DateTime? PrevFromUtc, DateTime? PrevToUtc, string? Error) ResolvePeriod(
            string? period, long? fromDate, long? toDate, string defaultPeriod = "all")
        {
            var p = (string.IsNullOrWhiteSpace(period) ? defaultPeriod : period).Trim().ToLowerInvariant();
            var nowVn = NowVietnam;

            switch (p)
            {
                case "today":
                {
                    var startVn = new DateTime(nowVn.Year, nowVn.Month, nowVn.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    var endVn = new DateTime(nowVn.Year, nowVn.Month, nowVn.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
                    var prevStartVn = startVn.AddDays(-1);
                    var prevEndVn = endVn.AddDays(-1);
                    return ("today",
                        ToUtcTime(startVn),
                        ToUtcTime(endVn),
                        ToUtcTime(prevStartVn),
                        ToUtcTime(prevEndVn),
                        null);
                }
                case "week":
                {
                    var startDayVn = nowVn.Date.AddDays(-6);
                    var startVn = new DateTime(startDayVn.Year, startDayVn.Month, startDayVn.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    var endVn = new DateTime(nowVn.Year, nowVn.Month, nowVn.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
                    var prevStartVn = startVn.AddDays(-7);
                    var prevEndVn = endVn.AddDays(-7);
                    return ("week",
                        ToUtcTime(startVn),
                        ToUtcTime(endVn),
                        ToUtcTime(prevStartVn),
                        ToUtcTime(prevEndVn),
                        null);
                }
                case "month":
                {
                    var startVn = new DateTime(nowVn.Year, nowVn.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    var daysInMonth = DateTime.DaysInMonth(nowVn.Year, nowVn.Month);
                    var endVn = new DateTime(nowVn.Year, nowVn.Month, daysInMonth, 23, 59, 59, 999, DateTimeKind.Unspecified);

                    var prevMonthDate = startVn.AddMonths(-1);
                    var prevDaysInMonth = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);
                    var prevStartVn = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    var prevEndVn = new DateTime(prevMonthDate.Year, prevMonthDate.Month, prevDaysInMonth, 23, 59, 59, 999, DateTimeKind.Unspecified);

                    return ("month",
                        ToUtcTime(startVn),
                        ToUtcTime(endVn),
                        ToUtcTime(prevStartVn),
                        ToUtcTime(prevEndVn),
                        null);
                }
                case "custom":
                {
                    if (fromDate is not > 0 || toDate is not > 0)
                        return ("custom", null, null, null, null, "Vui lòng truyền FromDate và ToDate hợp lệ khi chọn khoảng thời gian tùy chọn.");

                    var fromUtc = DateTimeOffset.FromUnixTimeSeconds(fromDate.Value).UtcDateTime;
                    var toUtc = DateTimeOffset.FromUnixTimeSeconds(toDate.Value).UtcDateTime;
                    if (fromUtc > toUtc)
                        return ("custom", null, null, null, null, "FromDate phải trước hoặc bằng ToDate.");

                    var duration = toUtc - fromUtc;
                    var prevToUtc = fromUtc;
                    var prevFromUtc = fromUtc - duration;

                    return ("custom", fromUtc, toUtc, prevFromUtc, prevToUtc, null);
                }
                case "all":
                default:
                    return ("all", null, null, null, null, null);
            }
        }
    }
}

