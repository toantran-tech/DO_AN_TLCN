using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace WashGo.Domain.Shared.Validation;

/// <summary>
/// Quy tắc định danh dùng chung cho đăng ký, tạo tài khoản bởi Admin và đổi email. Email luôn được chuẩn hóa
/// trim + chữ thường trước khi lưu/tra cứu; DB giữ unique trên lower(email) của tài khoản chưa xóa (cùng quy tắc).
/// </summary>
public static class AccountRules
{
    public const int EmailMaxLength = 100;
    public const int FullNameMaxLength = 100;
    public const int ReasonMaxLength = 500;
    public static readonly IReadOnlyList<string> Roles = ["Customer", "Merchant", "Shipper", "Admin"];

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? email)
    {
        var value = email?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > EmailMaxLength) return false;
        try { return string.Equals(new MailAddress(value).Address, value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    /// <summary>null khi hợp lệ; họ tên 2–100 ký tự, không chứa thẻ HTML.</summary>
    public static string? FullNameError(string? fullName)
    {
        var value = fullName?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length < 2 || value.Length > FullNameMaxLength)
            return "Họ tên phải có từ 2 đến 100 ký tự.";
        return value.Contains('<') || value.Contains('>') ? "Họ tên không được chứa ký tự < hoặc >." : null;
    }

    public static string? ReasonError(string? reason, bool required)
    {
        var value = reason?.Trim();
        if (string.IsNullOrEmpty(value)) return required ? "Vui lòng nhập lý do (tối đa 500 ký tự)." : null;
        return value.Length > ReasonMaxLength ? "Lý do tối đa 500 ký tự." : null;
    }

    public static string? CanonicalRole(string? role)
    {
        foreach (var name in Roles)
            if (string.Equals(name, role?.Trim(), StringComparison.OrdinalIgnoreCase)) return name;
        return null;
    }
}
