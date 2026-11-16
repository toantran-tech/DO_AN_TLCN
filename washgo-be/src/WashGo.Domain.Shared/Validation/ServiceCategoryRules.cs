using System.Text;
using System.Text.RegularExpressions;

namespace WashGo.Domain.Shared.Validation;

public static class ServiceCategoryRules
{
    public static Guid LegacyId(int serviceType) => serviceType is >= 1 and <= 5
        ? Guid.Parse($"ca000000-0000-0000-0000-{serviceType:000000000000}")
        : throw new ArgumentOutOfRangeException(nameof(serviceType), "Unknown legacy processing type.");
    public static string Name(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");
    public static string Key(string value) => Name(value).ToUpperInvariant();
    public static string? Validate(string? code, string? name, string? description, int sortOrder)
    {
        if (!Regex.IsMatch(code ?? "", "^[A-Za-z0-9_-]{2,30}$")) return "Code cần 2–30 chữ Latin, số, - hoặc _.";
        if (string.IsNullOrWhiteSpace(name) || Name(name).Length > 100 || name.Contains('<') || name.Contains('>')) return "Tên cần 1–100 ký tự, không dùng HTML.";
        if (description?.Length > 500 || description?.Contains('<') == true || description?.Contains('>') == true) return "Mô tả tối đa 500 ký tự, không dùng HTML.";
        return sortOrder is < 0 or > 100000 ? "sortOrder phải từ 0 đến 100000." : null;
    }
}
