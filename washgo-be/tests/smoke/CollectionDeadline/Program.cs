using WashGo.Domain.Shared.Scheduling;

// Collect deadline = SLA hours counted only inside the Merchant's [open, close) shifts, VN time (UTC+7), from DepositedAt.
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
DateTime Vn(int month, int day, int hour, int minute = 0) => new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);
OpeningSchedule Daily(string open, string close, params int[] days) => new()
{
    Weekly = (days.Length == 0 ? Enumerable.Range(1, 7) : days).Select(d => new OpeningDay { DayOfWeek = d, Shifts = [new() { Open = open, Close = close }] }).ToList()
};
DateTime? Deadline(OpeningSchedule s, DateTime depositedUtc, int sla = 4) => OpeningScheduleRules.Compute(depositedUtc, sla, s).DeadlineUtc;

var shop = Daily("08:00", "20:00");
// Examples from the requirement (2026-11-23 is a Monday).
Check(Deadline(shop, Vn(11, 23, 10)) == Vn(11, 23, 14), "10:00 -> 14:00 same day");
Check(Deadline(shop, Vn(11, 23, 18)) == Vn(11, 24, 10), "18:00 -> 2h today + 2h tomorrow = 10:00");
Check(Deadline(shop, Vn(11, 23, 21)) == Vn(11, 24, 12), "21:00 -> starts 08:00, deadline 12:00");
Check(Deadline(shop, Vn(11, 23, 20)) == Vn(11, 24, 12), "Exactly at closing [open, close) -> next shift");
Check(Deadline(shop, Vn(11, 23, 8)) == Vn(11, 23, 12), "Exactly at opening counts at once");
Check(Deadline(shop, Vn(11, 23, 6)) == Vn(11, 23, 12), "Before opening waits for 08:00");
Check(OpeningScheduleRules.Compute(Vn(11, 23, 21), 4, shop).StartsAtUtc == Vn(11, 24, 8), "Start of counting is the next shift");
Check(Deadline(shop, Vn(11, 23, 16), 4) == Vn(11, 23, 20), "Deadline may end exactly at closing");
Check(Deadline(shop, Vn(11, 23, 10), 30) == Vn(11, 25, 16), "SLA longer than one shift: 10h + 12h + 8h");
// UTC day change: 23:30 VN on the 23rd is 16:30 UTC the same day; 01:00 VN on the 24th is 18:00 UTC on the 23rd.
Check(Deadline(shop, Vn(11, 24, 1)) == Vn(11, 24, 12), "Local date used after UTC midnight shift");
// Lunch break.
var lunch = new OpeningSchedule { Weekly = Enumerable.Range(1, 7).Select(d => new OpeningDay { DayOfWeek = d, Shifts = [new() { Open = "08:00", Close = "12:00" }, new() { Open = "13:00", Close = "17:00" }] }).ToList() };
Check(Deadline(lunch, Vn(11, 23, 10)) == Vn(11, 23, 15), "Lunch break skipped: 2h + 2h after 13:00");
// Weekend closed (Mon–Fri): Friday 18:00 -> Monday 10:00.
var weekdays = Daily("08:00", "20:00", 1, 2, 3, 4, 5);
Check(Deadline(weekdays, Vn(11, 27, 18)) == Vn(11, 30, 10), "Weekend skipped");
// Holiday exception.
var holiday = Daily("08:00", "20:00"); holiday.ClosedDates = ["2026-11-24"];
Check(Deadline(holiday, Vn(11, 23, 21)) == Vn(11, 25, 12), "Closed date skipped");
// Night shift split over midnight (22:00-24:00 + 00:00-06:00).
var night = new OpeningSchedule { Weekly = Enumerable.Range(1, 7).Select(d => new OpeningDay { DayOfWeek = d, Shifts = [new() { Open = "00:00", Close = "06:00" }, new() { Open = "22:00", Close = "24:00" }] }).ToList() };
Check(Deadline(night, Vn(11, 23, 23)) == Vn(11, 24, 3), "Shift across midnight via 24:00 + 00:00");
// 24/7 only when explicit.
Check(Deadline(new OpeningSchedule { AlwaysOpen = true }, Vn(11, 23, 23)) == Vn(11, 24, 3), "Explicit 24/7 is continuous");
// Long closure: no infinite loop, no fake deadline.
var none = OpeningScheduleRules.Compute(Vn(11, 23, 7), 400 * 24, Daily("08:00", "09:00"));
Check(none.DeadlineUtc == null, "No deadline when shifts in the search window cannot cover the SLA");

// Validation and required hours.
Check(OpeningScheduleRules.Validate(null)?.Code == OpeningScheduleRules.Required, "Missing hours rejected");
Check(OpeningScheduleRules.Validate(new OpeningSchedule())?.Code == OpeningScheduleRules.NoOpenDay, "No open day rejected");
Check(OpeningScheduleRules.Validate(Daily("08:00", "08:00"))?.Code == OpeningScheduleRules.InvalidShift, "open == close is not 24h");
Check(OpeningScheduleRules.Validate(Daily("20:00", "08:00"))?.Code == OpeningScheduleRules.InvalidShift, "Overnight shift must be split");
Check(OpeningScheduleRules.Validate(Daily("8:00", "20:00"))?.Code == OpeningScheduleRules.InvalidFormat, "HH:mm required");
Check(OpeningScheduleRules.Validate(Daily("08:00", "24:01"))?.Code == OpeningScheduleRules.InvalidFormat, "24:01 rejected");
var overlap = new OpeningSchedule { Weekly = [new() { DayOfWeek = 1, Shifts = [new() { Open = "08:00", Close = "12:00" }, new() { Open = "11:00", Close = "14:00" }] }] };
Check(OpeningScheduleRules.Validate(overlap)?.Code == OpeningScheduleRules.Overlap, "Overlap rejected");
Check(OpeningScheduleRules.Validate(new OpeningSchedule { TimeZone = "UTC", AlwaysOpen = true })?.Code == OpeningScheduleRules.UnsupportedTimeZone, "Time zone enforced");
Check(OpeningScheduleRules.Validate(new OpeningSchedule { AlwaysOpen = true, Weekly = shop.Weekly })?.Code == OpeningScheduleRules.InvalidFormat, "24/7 with shifts rejected");
Check(OpeningScheduleRules.Validate(new OpeningSchedule { Weekly = [new() { DayOfWeek = 8, Shifts = shop.Weekly[0].Shifts }] })?.Code == OpeningScheduleRules.InvalidFormat, "Day 8 rejected");

// Legacy text: only exactly computable forms.
Check(OpeningScheduleRules.FromLegacy("\"07:00-22:00\"")?.Weekly.Count == 7, "Quoted daily range parsed");
Check(OpeningScheduleRules.FromLegacy("{\"mon\":\"06:00-12:00,13:00-23:00\",\"sun\":\"closed\"}") is { } map && map.Weekly.Count == 1 && map.Weekly[0].Shifts.Count == 2, "Per-day JSON parsed");
Check(OpeningScheduleRules.FromLegacy("Sáng 7h đến tối") == null, "Free text not guessed");
Check(OpeningScheduleRules.FromLegacy(null) == null && OpeningScheduleRules.FromLegacy("") == null, "Empty is not 24/7");
Check(OpeningScheduleRules.Display(shop) == "Hằng ngày 08:00-20:00", "Display daily");
Check(OpeningScheduleRules.Version(shop) == OpeningScheduleRules.Version(OpeningScheduleRules.Deserialize(OpeningScheduleRules.Serialize(shop))!), "Version stable across round trip");
Console.WriteLine("PASS: collect deadline counts only open [open, close) shifts in VN time from the deposit (examples 10:00/18:00/21:00/20:00, lunch, weekend, holiday, midnight split, explicit 24/7, bounded search), validation codes and exact legacy parsing.");
