namespace WashGo.Domain.Shared.Validation;

/// <summary>
/// Mã lưu giữ của vòng kho: mã mới STdddd (theo quy tắc mã nghiệp vụ); mã đã phát hành trước đó
/// (LK + thời gian Việt Nam + 4 số) vẫn hợp lệ và không đổi.
/// </summary>
public static class StorageReferenceRules
{
    public static string Format(int number) => BusinessCodeRules.Format(BusinessCodeRules.Storage, number);

    public static bool IsIssuedFormat(string? code) =>
        BusinessCodeRules.IsGenerated(code, BusinessCodeRules.Storage) ||
        code is { Length: 20 } && code.StartsWith("LK", StringComparison.Ordinal) && code[2..].All(char.IsAsciiDigit);
}
