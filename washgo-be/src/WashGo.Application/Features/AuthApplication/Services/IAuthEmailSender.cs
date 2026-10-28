using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services;

/// <summary>
/// Delivers a one-time password without exposing it through the API response.
/// </summary>
public interface IAuthEmailSender
{
    Task<bool> SendOtpAsync(string email, string code, OtpPurpose purpose);
}
