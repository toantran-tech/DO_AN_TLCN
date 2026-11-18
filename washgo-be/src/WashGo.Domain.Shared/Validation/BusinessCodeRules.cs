using System.Globalization;
using System.Text.RegularExpressions;

namespace WashGo.Domain.Shared.Validation;

/// <summary>
/// Mã nghiệp vụ do hệ thống cấp cho bản ghi mới: 2 chữ cái in hoa + đúng 4 chữ số (10.000 mã mỗi tiền tố).
/// Mã cũ/seed giữ nguyên; mã không đổi sau khi tạo.
/// </summary>
public static class BusinessCodeRules
{
    public const string Service = "SV";
    public const string Category = "DM";
    public const string Locker = "LK";
    public const string ServiceArea = "KV";
    public const string Storage = "ST";
    public const int Space = 10000;

    public const string ImmutableCode = "CodeImmutable";
    public const string ImmutableMessage = "Mã do hệ thống cấp và không thể thay đổi; bỏ trường code hoặc gửi đúng mã hiện tại.";

    public static string Format(string prefix, int number) =>
        number is >= 0 and < Space && Regex.IsMatch(prefix, "^[A-Z]{2}$")
            ? prefix + number.ToString("D4", CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(nameof(number));

    public static bool IsGenerated(string? code, string prefix) =>
        code != null && code.Length == 6 && code.StartsWith(prefix, StringComparison.Ordinal) && code[2..].All(char.IsAsciiDigit);

    /// <summary>Khi sửa: không gửi mã, hoặc gửi đúng mã đã lưu (kể cả mã cũ); mã khác bị từ chối.</summary>
    public static bool AcceptsOnUpdate(string? sent, string stored) =>
        string.IsNullOrWhiteSpace(sent) || string.Equals(sent.Trim(), stored, StringComparison.OrdinalIgnoreCase);
}
