using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services;

/// <summary>
/// Delivers a one-time password without exposing it through the API response.
/// </summary>
public interface IAuthEmailSender
{
    Task<bool> SendOtpAsync(string email, string code, OtpPurpose purpose);

    /// <summary>Gửi thông tin đăng nhập cho tài khoản do Merchant/Admin tạo (UC 2.4.2).</summary>
    Task<bool> SendAccountCredentialsAsync(string email, string fullName, string temporaryPassword);
    /// <summary>Merchant credential message; Sent means SMTP accepted, Unknown is not safe to auto-retry.</summary>
    Task<CredentialDeliveryStatus> SendMerchantCredentialsAsync(string email, string fullName, string temporaryPassword);
}
