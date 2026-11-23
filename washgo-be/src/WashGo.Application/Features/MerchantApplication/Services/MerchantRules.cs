using System;
using System.Linq;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using WashGo.Domain.Shared.Scheduling;

namespace WashGo.Application.Features.MerchantApplication.Services
{
    /// <summary>Kiểm tra dùng chung cho hồ sơ Merchant: Admin quản lý (UC 3.2) và Merchant tự cập nhật (UC 2.1.2).</summary>
    public static class MerchantRules
    {
        public const string InvalidPhone = "Số điện thoại không hợp lệ.";
        public const string InvalidCoordinates = "Tọa độ không hợp lệ.";
        public const string InvalidCommission = "Tỷ lệ chiết khấu phải từ 0-100%.";

        private static readonly Regex PhonePattern = new(@"^\+?[0-9 ()\-.]+$", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions HoursJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>Trả về số điện thoại chỉ gồm chữ số (9–11 số), hoặc null nếu không hợp lệ.</summary>
        public static string? NormalizePhone(string? phone)
        {
            var text = phone?.Trim() ?? string.Empty;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return text.Length > 20 || !PhonePattern.IsMatch(text) || digits.Length is < 9 or > 11 ? null : digits;
        }

        public static bool ValidCoordinates(decimal latitude, decimal longitude) =>
            latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && !(latitude == 0 && longitude == 0);

        public static bool IsValidEmail(string email)
        {
            if (email.Length is 0 or > 100) return false;
            try
            {
                return string.Equals(new MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // opening_hours and business_license are jsonb columns: keep valid JSON (compacted), store plain text such as "07:00-22:00" as a JSON string.
        /// <summary>
        /// Opening hours for a write. A structured schedule wins; otherwise the text must be exactly computable
        /// ("HH:mm-HH:mm" or {"mon":"HH:mm-HH:mm",…}) unless it is the unchanged current text; nothing sent keeps the
        /// current valid schedule. Never guesses hours or falls back to 24/7.
        /// </summary>
        public static (OpeningSchedule? Schedule, OpeningScheduleRules.Problem? Problem, bool Structured) ResolveSchedule(
            OpeningSchedule? structured, string? text, OpeningSchedule? current, string? currentText)
        {
            if (structured != null)
                return OpeningScheduleRules.Validate(structured) is { } problem ? (null, problem, true) : (OpeningScheduleRules.Normalize(structured), null, true);
            if (!string.IsNullOrWhiteSpace(text) && NormalizeJson(text) != NormalizeJson(currentText))
                return OpeningScheduleRules.FromLegacy(text) is { } parsed ? (parsed, null, false)
                    : (null, new(OpeningScheduleRules.InvalidFormat, "Giờ mở cửa dạng chữ phải là \"HH:mm-HH:mm\" hoặc theo từng ngày {\"mon\":\"HH:mm-HH:mm\"}; hãy gửi openingSchedule có cấu trúc."), false);
            return current != null ? (current, null, false) : (null, OpeningScheduleRules.Validate(null), false);
        }

        public static string? NormalizeJson(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var text = value.Trim();
            try
            {
                using var doc = JsonDocument.Parse(text);
                return JsonSerializer.Serialize(doc.RootElement, HoursJson);
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(text, HoursJson);
            }
        }
    }
}
