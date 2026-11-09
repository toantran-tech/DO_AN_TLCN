namespace WashGo.Domain.Entities.Partner;

/// <summary>Contract end is an inclusive Vietnamese calendar date; null is unlimited.</summary>
public static class MerchantContractRules
{
    /// <summary>Date-only JSON is a VN calendar date; timestamp JSON is first converted to its VN date.</summary>
    public static DateTime? NormalizeDate(DateTime? input)
    {
        if (!input.HasValue) return null;
        var value = input.Value;
        var vnDate = value.Kind == DateTimeKind.Unspecified ? value.Date : value.ToUniversalTime().AddHours(7).Date;
        return DateTime.SpecifyKind(vnDate.AddHours(-7), DateTimeKind.Utc);
    }
    public static DateTime? ExpiresAtUtc(DateTime? end) => end?.AddHours(7).Date.AddDays(1).AddHours(-7);
    public static bool IsExpired(DateTime? end, DateTime now) => ExpiresAtUtc(end) is { } expiry && now >= expiry;
}
