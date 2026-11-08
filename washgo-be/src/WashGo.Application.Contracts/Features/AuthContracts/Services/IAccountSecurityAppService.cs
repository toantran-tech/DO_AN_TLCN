using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AuthContracts.Services
{
    /// <summary>Bảo mật tài khoản của chính người đang đăng nhập (mọi vai trò); userId/sessionId lấy từ JWT.</summary>
    public interface IAccountSecurityAppService : IApplicationService
    {
        Task<BaseResponse<SecurityStatusDto>> GetStatusAsync(Guid userId, Guid sessionId);
        Task<BaseResponse<bool>> ChangePasswordAsync(Guid userId, Guid sessionId, ChangePasswordCommand command);
        Task<BaseResponse<List<SessionDto>>> GetSessionsAsync(Guid userId, Guid sessionId);
        Task<BaseResponse<RevokeSessionsResultDto>> RevokeOtherSessionsAsync(Guid userId, Guid sessionId);
        Task<BaseResponse<RevokeSessionsResultDto>> RevokeSessionAsync(Guid userId, Guid sessionId, Guid targetId);
    }
}
