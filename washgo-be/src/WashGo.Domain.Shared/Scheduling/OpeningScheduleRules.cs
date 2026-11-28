using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WashGo.Domain.Shared.Scheduling;

/// <summary>Validation, legacy parsing, display and the collect-deadline calculation for <see cref="OpeningSchedule"/>.</summary>
public static class OpeningScheduleRules
{
    public const string SupportedTimeZone = "Asia/Ho_Chi_Minh";
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public const int MaxShiftsPerDay = 4;
    public const int MaxClosedDates = 60;
    /// <summary>Upper bound of the shift search so a long closure never loops forever.</summary>
    public const int MaxSearchDays = 400;

    public const string Required = "OpeningHoursRequired";
    public const string InvalidFormat = "OpeningHoursInvalidFormat";
    public const string InvalidShift = "OpeningHoursInvalidShift";
    public const string Overlap = "OpeningHoursOverlap";
    public const string NoOpenDay = "OpeningHoursNoOpenDay";
    public const string UnsupportedTimeZone = "OpeningHoursUnsupportedTimeZone";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] DayKeys = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
    private static readonly string[] DayLabels = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
    private static readonly Regex Range = new(@"^\s*(\d{2}:\d{2})\s*-\s*(\d{2}:\d{2})\s*$", RegexOptions.CultureInvariant);

    public sealed record Problem(string Code, string Message);

    /// <summary>Null when valid. Rules: VN time zone, 24/7 or at least one open weekday, 1–4 non-overlapping [open, close) shifts per day.</summary>
    public static Problem? Validate(OpeningSchedule? schedule)
    {
        if (schedule == null)
            return new(Required, "Giờ mở cửa là bắt buộc và được dùng để tính hạn thu gom.");
        if (schedule.TimeZone != SupportedTimeZone)
            return new(UnsupportedTimeZone, $"Chỉ hỗ trợ múi giờ {SupportedTimeZone}.");
        if ((schedule.ClosedDates?.Count ?? 0) > MaxClosedDates ||
            schedule.ClosedDates?.Any(x => !DateOnly.TryParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) == true)
            return new(InvalidFormat, $"Ngày nghỉ phải theo dạng yyyy-MM-dd, tối đa {MaxClosedDates} ngày.");
        var weekly = schedule.Weekly ?? [];
        if (schedule.AlwaysOpen)
            return weekly.Count > 0 ? new(InvalidFormat, "Lịch 24/7 không kèm danh sách ca.") : null;
        if (weekly.Any(d => d.DayOfWeek is < 1 or > 7) || weekly.Select(d => d.DayOfWeek).Distinct().Count() != weekly.Count)
            return new(InvalidFormat, "Mỗi ngày trong tuần (1 = Thứ Hai … 7 = Chủ Nhật) chỉ khai báo một lần.");
        foreach (var day in weekly)
        {
            var shifts = day.Shifts ?? [];
            if (shifts.Count > MaxShiftsPerDay)
                return new(InvalidFormat, $"Tối đa {MaxShiftsPerDay} ca mỗi ngày.");
            var parsed = new List<(TimeSpan Open, TimeSpan Close)>();
            foreach (var shift in shifts)
            {
                if (ParseTime(shift.Open, false) is not { } open || ParseTime(shift.Close, true) is not { } close)
                    return new(InvalidFormat, "Giờ phải theo dạng HH:mm (00:00–23:59; giờ đóng có thể là 24:00).");
                if (open >= close)
                    return new(InvalidShift, "Giờ mở phải trước giờ đóng; giờ mở bằng giờ đóng không được hiểu là 24 giờ, ca qua nửa đêm cần tách thành hai ngày.");
                parsed.Add((open, close));
            }
            var ordered = parsed.OrderBy(x => x.Open).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].Open < ordered[i - 1].Close)
                    return new(Overlap, "Các ca trong cùng một ngày không được chồng lấn.");
        }
        return weekly.Any(d => (d.Shifts?.Count ?? 0) > 0) ? null : new(NoOpenDay, "Cần ít nhất một ngày có ca mở cửa.");
    }

    /// <summary>Sorted days/shifts, distinct sorted closed dates; call after <see cref="Validate"/>.</summary>
    public static OpeningSchedule Normalize(OpeningSchedule schedule) => new()
    {
        TimeZone = SupportedTimeZone,
        AlwaysOpen = schedule.AlwaysOpen,
        Weekly = schedule.AlwaysOpen ? [] : (schedule.Weekly ?? []).Where(d => (d.Shifts?.Count ?? 0) > 0).OrderBy(d => d.DayOfWeek)
            .Select(d => new OpeningDay
            {
                DayOfWeek = d.DayOfWeek,
                Shifts = d.Shifts.OrderBy(s => ParseTime(s.Open, false)).Select(s => new OpeningShift
                    { Open = Format(ParseTime(s.Open, false)!.Value), Close = Format(ParseTime(s.Close, true)!.Value) }).ToList()
            }).ToList(),
        ClosedDates = (schedule.ClosedDates ?? []).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList()
    };

    /// <summary>
    /// Reads an existing free-text value only when it is exactly computable: "HH:mm-HH:mm" (every day) or a JSON object
    /// {"mon": "HH:mm-HH:mm[,HH:mm-HH:mm]", …, "sun": …} (missing/"closed" days are closed). Anything else returns null.
    /// </summary>
    public static OpeningSchedule? FromLegacy(string? openingHours)
    {
        if (string.IsNullOrWhiteSpace(openingHours)) return null;
        var text = openingHours.Trim();
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.String) text = doc.RootElement.GetString() ?? string.Empty;
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var days = new List<OpeningDay>();
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var index = Array.IndexOf(DayKeys, property.Name.ToLowerInvariant());
                    if (index < 0 || property.Value.ValueKind != JsonValueKind.String) return null;
                    var value = property.Value.GetString()?.Trim() ?? string.Empty;
                    if (value.Equals("closed", StringComparison.OrdinalIgnoreCase)) continue;
                    var shifts = value.Split(',').Select(ParseRange).ToList();
                    if (shifts.Any(x => x == null)) return null;
                    days.Add(new OpeningDay { DayOfWeek = index + 1, Shifts = shifts.Select(x => x!).ToList() });
                }
                var parsed = new OpeningSchedule { Weekly = days };
                return Validate(parsed) == null ? Normalize(parsed) : null;
            }
            else return null;
        }
        catch (JsonException) { }
        var daily = ParseRange(text);
        if (daily == null) return null;
        var schedule = new OpeningSchedule
        {
            Weekly = Enumerable.Range(1, 7).Select(d => new OpeningDay { DayOfWeek = d, Shifts = [new() { Open = daily.Open, Close = daily.Close }] }).ToList()
        };
        return Validate(schedule) == null ? Normalize(schedule) : null;
    }

    public static string Serialize(OpeningSchedule schedule) => JsonSerializer.Serialize(Normalize(schedule), Json);

    public static bool Equivalent(OpeningSchedule? left, OpeningSchedule? right)
    {
        if (left == null || right == null) return left == null && right == null;
        return Validate(left) == null && Validate(right) == null && Serialize(left) == Serialize(right);
    }

    public static OpeningSchedule? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var schedule = JsonSerializer.Deserialize<OpeningSchedule>(json, Json);
            return schedule != null && Validate(schedule) == null ? Normalize(schedule) : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Short content hash of the normalized schedule, stored on orders as the applied schedule version.</summary>
    public static string Version(OpeningSchedule schedule) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(schedule))))[..16].ToLowerInvariant();

    /// <summary>Human-readable summary (kept in Merchant.OpeningHours for display/compatibility).</summary>
    public static string Display(OpeningSchedule schedule)
    {
        var s = Normalize(schedule);
        if (s.AlwaysOpen) return "Mở cửa 24/7" + Closed(s);
        var groups = s.Weekly.GroupBy(d => string.Join(", ", d.Shifts.Select(x => $"{x.Open}-{x.Close}")))
            .Select(g => (Days: g.Select(d => d.DayOfWeek).ToList(), Hours: g.Key)).ToList();
        if (groups.Count == 1 && groups[0].Days.Count == 7) return $"Hằng ngày {groups[0].Hours}" + Closed(s);
        return string.Join("; ", groups.Select(g => $"{string.Join(", ", g.Days.Select(d => DayLabels[d - 1]))} {g.Hours}")) + Closed(s);
    }

    /// <summary>
    /// Adds <paramref name="slaHours"/> of open time to <paramref name="depositedAtUtc"/>: counting starts at once inside a
    /// shift, otherwise at the next shift start; a shift ending before the SLA is used up carries the rest to the next one.
    /// Shifts are [open, close), so a deposit exactly at closing time starts at the next shift. Closed dates and days
    /// without shifts are skipped. Returns a null deadline when no shift exists within <see cref="MaxSearchDays"/>.
    /// </summary>
    public static CollectionWindow Compute(DateTime depositedAtUtc, int slaHours, OpeningSchedule schedule)
    {
        var deposited = DateTime.SpecifyKind(depositedAtUtc, DateTimeKind.Utc);
        if (schedule.AlwaysOpen) return new(deposited, deposited.AddHours(slaHours));
        var local = DateTime.SpecifyKind(deposited + Offset, DateTimeKind.Unspecified);
        var remaining = TimeSpan.FromHours(slaHours);
        var closed = (schedule.ClosedDates ?? []).ToHashSet();
        var weekly = (schedule.Weekly ?? []).ToDictionary(d => d.DayOfWeek,
            d => d.Shifts.Select(x => (Open: ParseTime(x.Open, false)!.Value, Close: ParseTime(x.Close, true)!.Value)).OrderBy(x => x.Open).ToList());
        DateTime? start = null;
        for (var day = 0; day <= MaxSearchDays; day++)
        {
            var date = local.Date.AddDays(day);
            if (closed.Contains(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) continue;
            var iso = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            if (!weekly.TryGetValue(iso, out var shifts)) continue;
            foreach (var (open, close) in shifts)
            {
                var shiftEnd = date + close;
                if (shiftEnd <= local) continue;
                var segmentStart = date + open > local ? date + open : local;
                start ??= segmentStart;
                var available = shiftEnd - segmentStart;
                if (remaining <= available)
                    return new(ToUtc(start.Value), ToUtc(segmentStart + remaining));
                remaining -= available;
            }
        }
        return new(start.HasValue ? ToUtc(start.Value) : null, null);
    }

    private static DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local - Offset, DateTimeKind.Utc);

    private static string Closed(OpeningSchedule s) => s.ClosedDates.Count == 0 ? string.Empty : $"; nghỉ {string.Join(", ", s.ClosedDates)}";

    private static OpeningShift? ParseRange(string value)
    {
        var match = Range.Match(value);
        return match.Success ? new OpeningShift { Open = match.Groups[1].Value, Close = match.Groups[2].Value } : null;
    }

    private static TimeSpan? ParseTime(string? value, bool allowEndOfDay)
    {
        if (value == null || value.Length != 5 || value[2] != ':' ||
            !int.TryParse(value[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var h) ||
            !int.TryParse(value[3..], NumberStyles.None, CultureInfo.InvariantCulture, out var m))
            return null;
        if (allowEndOfDay && h == 24 && m == 0) return TimeSpan.FromHours(24);
        return h is >= 0 and <= 23 && m is >= 0 and <= 59 ? new TimeSpan(h, m, 0) : null;
    }

    private static string Format(TimeSpan t) => t == TimeSpan.FromHours(24) ? "24:00" : $"{(int)t.TotalHours:00}:{t.Minutes:00}";
}
