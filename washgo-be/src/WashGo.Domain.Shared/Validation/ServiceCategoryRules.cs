using System.Text;
using System.Text.RegularExpressions;

namespace WashGo.Domain.Shared.Validation;

public static class ServiceCategoryRules
{
    public static Guid LegacyId(int serviceType) => serviceType is >= 1 and <= 5
        ? Guid.Parse($"ca000000-0000-0000-0000-{serviceType:000000000000}")
        : throw new ArgumentOutOfRangeException(nameof(serviceType), "Unknown legacy processing type.");
    /// <summary>Loại xử lý legacy 1–5 của một trong 5 danh mục gốc; null với mọi danh mục khác (không suy từ tên/mã/thứ tự).</summary>
    public static int? LegacyType(Guid categoryId)
    {
        for (var type = 1; type <= 5; type++)
            if (LegacyId(type) == categoryId) return type;
        return null;
    }
    public static string Name(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");
    public static string Key(string value) => Name(value).ToUpperInvariant();
    /// <summary>Kiểm tra tên/mô tả/thứ tự; mã danh mục do hệ thống cấp nên không còn là dữ liệu nhập.</summary>
    public static string? Validate(string? name, string? description, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || Name(name).Length > 100 || name.Contains('<') || name.Contains('>')) return "Tên cần 1–100 ký tự, không dùng HTML.";
        if (description?.Length > 500 || description?.Contains('<') == true || description?.Contains('>') == true) return "Mô tả tối đa 500 ký tự, không dùng HTML.";
        return sortOrder is < 0 or > 100000 ? "sortOrder phải từ 0 đến 100000." : null;
    }
}
