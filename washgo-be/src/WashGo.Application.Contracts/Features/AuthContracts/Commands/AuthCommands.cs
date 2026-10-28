using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AuthContracts.Commands
{
    public class LoginCommand
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class GoogleLoginCommand
    {
        /// <summary>
        /// Google ID token returned by Google Identity Services in the browser.
        /// </summary>
        public string IdToken { get; set; } = string.Empty;
    }

    public class LinkGoogleCommand : GoogleLoginCommand
    {
        /// <summary>
        /// Re-authentication proof before a Google identity is attached to the current account.
        /// </summary>
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class RefreshTokenCommand
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutCommand
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RegisterCommand
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string VerificationToken { get; set; } = string.Empty;
    }

    public class SendOtpCommand
    {
        public string Email { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; } = OtpPurpose.ForgotPassword;
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    public class VerifyOtpCommand
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; } = OtpPurpose.ForgotPassword;
    }

    public class ResetPasswordCommand
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
