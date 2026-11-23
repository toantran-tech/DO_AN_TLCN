using System;
using System.Collections.Generic;

namespace WashGo.Domain.Shared.Scheduling;

/// <summary>
/// Structured Merchant opening hours, also the collection calendar for the collect SLA.
/// Times are local to <see cref="TimeZone"/> (only Asia/Ho_Chi_Minh, UTC+7, no DST). Each shift is [open, close);
/// close may be "24:00" for end of day. A shift never crosses midnight: use 22:00–24:00 plus 00:00–06:00 on the next day.
/// </summary>
public class OpeningSchedule
{
    public string TimeZone { get; set; } = OpeningScheduleRules.SupportedTimeZone;
    /// <summary>Explicit 24/7. Never inferred from missing data.</summary>
    public bool AlwaysOpen { get; set; }
    /// <summary>One entry per open weekday; dayOfWeek 1 = Monday … 7 = Sunday. Missing days are closed.</summary>
    public List<OpeningDay> Weekly { get; set; } = [];
    /// <summary>Local dates (yyyy-MM-dd) when the Merchant does not collect (holidays, exceptions).</summary>
    public List<string> ClosedDates { get; set; } = [];
}

public class OpeningDay
{
    public int DayOfWeek { get; set; }
    public List<OpeningShift> Shifts { get; set; } = [];
}

public class OpeningShift
{
    public string Open { get; set; } = string.Empty;
    public string Close { get; set; } = string.Empty;
}

/// <summary>Collection deadline states published on orders.</summary>
public static class CollectionDeadlineStates
{
    /// <summary>Deadline computed from DepositedAt within the Merchant's opening shifts.</summary>
    public const string Scheduled = "Scheduled";
    /// <summary>Deposited, no Merchant yet: computed (from DepositedAt) when the Merchant is assigned.</summary>
    public const string AwaitingMerchant = "AwaitingMerchant";
    /// <summary>The Merchant has no valid opening schedule; computed (from DepositedAt) once the Admin adds one.</summary>
    public const string ScheduleMissing = "ScheduleMissing";
    /// <summary>No opening shift in the next 400 days (long closure).</summary>
    public const string NoUpcomingShift = "NoUpcomingShift";
    /// <summary>Order deposited before the calendar rule: DepositedAt + SLA hours as promised then.</summary>
    public const string Legacy = "Legacy";
}

public sealed record CollectionWindow(DateTime? StartsAtUtc, DateTime? DeadlineUtc);
