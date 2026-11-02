using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AuthContracts.Services
{
    public interface IAuthAppService : IApplicationService
    {
        Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginCommand command);
        Task<BaseResponse<AuthResponseDto>> GoogleLoginAsync(GoogleLoginCommand command);
        Task<BaseResponse<AuthResponseDto>> GoogleAdminLoginAsync(GoogleLoginCommand command);
        Task<BaseResponse<bool>> LinkGoogleAsync(Guid userId, LinkGoogleCommand command);
        Task<BaseResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenCommand command);
        Task<BaseResponse<bool>> LogoutAsync(Guid userId, Guid sessionId, LogoutCommand command);
        Task<BaseResponse<RegisterResultDto>> RegisterAsync(RegisterCommand command);
        Task<BaseResponse<bool>> SendOtpAsync(SendOtpCommand command);
        Task<BaseResponse<VerifyOtpResultDto>> VerifyOtpAsync(VerifyOtpCommand command);
        Task<BaseResponse<bool>> ResetPasswordAsync(ResetPasswordCommand command);
        Task<BaseResponse<AuthUserDto>> GetCurrentUserAsync(Guid userId);
    }
}
