using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AuthContracts.Commands
{
    public class LoginCommand
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
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
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string RoleName { get; set; } = "Customer";
    }

    public class SendOtpCommand
    {
        public string Email { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; } = OtpPurpose.ForgotPassword;
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
    }
}

namespace WashGo.Application.Contracts.Features.AuthContracts.Results
{
    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public AuthUserDto User { get; set; } = null!;
    }

    public class AuthUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Avatar { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.AuthContracts.Services
{
    public interface IAuthAppService : IApplicationService
    {
        Task<BaseResponse<Results.AuthResponseDto>> LoginAsync(Commands.LoginCommand command);
        Task<BaseResponse<Results.AuthResponseDto>> RefreshTokenAsync(Commands.RefreshTokenCommand command);
        Task<BaseResponse<bool>> LogoutAsync(Commands.LogoutCommand command);
        Task<BaseResponse<Results.AuthResponseDto>> RegisterAsync(Commands.RegisterCommand command);
        Task<BaseResponse<bool>> SendOtpAsync(Commands.SendOtpCommand command);
        Task<BaseResponse<bool>> VerifyOtpAsync(Commands.VerifyOtpCommand command);
        Task<BaseResponse<bool>> ResetPasswordAsync(Commands.ResetPasswordCommand command);
        Task<BaseResponse<Results.AuthUserDto>> GetCurrentUserAsync(Guid userId);
    }
}
