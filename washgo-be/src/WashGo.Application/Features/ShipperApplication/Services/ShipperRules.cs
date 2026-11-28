using System;
using System.Net.Mail;
using System.Text.RegularExpressions;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;

namespace WashGo.Application.Features.ShipperApplication.Services
{
    /// <summary>Kiểm tra dùng chung: Merchant/Admin quản lý Shipper (UC 2.4) và Shipper tự cập nhật hồ sơ (UC 4.1.4).</summary>
    public static class ShipperRules
    {
        private static readonly Regex PlatePattern = new(@"^[0-9A-Z][0-9A-Z.\- ]{3,19}$", RegexOptions.Compiled);

        public static (string? Error, string FullName, string Phone, string Email, string? Plate) Validate(
            string? fullName, string? phone, string? email, VehicleType vehicleType, string? plate)
        {
            var name = fullName?.Trim() ?? string.Empty;
            var normalizedPhone = phone?.Trim() ?? string.Empty;
            var mail = email?.Trim().ToLowerInvariant() ?? string.Empty;
            var normalizedPlate = string.IsNullOrWhiteSpace(plate) ? null : plate.Trim().ToUpperInvariant();
            string? error = null;
            if (name.Length == 0)
                error = "Vui lòng nhập họ tên Shipper.";
            else if (name.Length > 100)
                error = "Họ tên tối đa 100 ký tự.";
            else if (!PhoneRules.IsValid(normalizedPhone))
                error = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
            else if (!IsValidEmail(mail))
                error = "Email không hợp lệ.";
            else if (!Enum.IsDefined(vehicleType))
                error = "Phương tiện không hợp lệ.";
            else if (vehicleType != VehicleType.Bicycle && (normalizedPlate == null || !PlatePattern.IsMatch(normalizedPlate)))
                error = "Biển số xe không hợp lệ.";
            else if (vehicleType == VehicleType.Bicycle && normalizedPlate != null && !PlatePattern.IsMatch(normalizedPlate))
                error = "Biển số xe không hợp lệ.";
            return (error, name, normalizedPhone, mail, normalizedPlate);
        }

        private static bool IsValidEmail(string email)
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
    }
}
