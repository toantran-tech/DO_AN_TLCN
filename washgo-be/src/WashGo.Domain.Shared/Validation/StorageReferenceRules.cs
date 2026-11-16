using System.Globalization;
namespace WashGo.Domain.Shared.Validation;
public static class StorageReferenceRules
{
    public static string Format(DateTime utc, int suffix)
    {
        if (utc.Kind != DateTimeKind.Utc || suffix is < 0 or > 9999) throw new ArgumentOutOfRangeException(nameof(suffix));
        return "LK" + utc.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + suffix.ToString("D4", CultureInfo.InvariantCulture);
    }
}
