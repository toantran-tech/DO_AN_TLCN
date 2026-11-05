using System.Text.RegularExpressions;

namespace WashGo.Domain.Shared.Validation;

public static class CustomerPhoneRules
{
    // Do not strip arbitrary characters: abc0912345678 must never become a valid phone.
    public static bool IsValid(string? phone) =>
        phone != null && Regex.IsMatch(phone.Trim(), @"\A0[0-9]{9}\z");
}
