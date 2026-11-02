using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace WashGo.Application.Features.AuthApplication.Services
{
    /// <summary>Mật khẩu tạm cho tài khoản do Admin/Merchant tạo (Shipper, Merchant), gửi qua email.</summary>
    public static class TemporaryPassword
    {
        // Meets the account password policy: upper, lower and digit, 12 characters.
        public static string Generate()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string all = upper + lower + digits;
            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)]
            };
            while (chars.Count < 12)
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
        }
    }
}
