using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Results;
using WashGo.Application.Contracts.Features.AuthContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services
{
    public class AccountSecurityAppService : WashGoAppService, IAccountSecurityAppService
    {
        /// <summary>Wrong current passwords allowed per account in the window before change-password is blocked.</summary>
        private const int MaxFailedChanges = 5;
        private static readonly TimeSpan FailedWindow = TimeSpan.FromMinutes(15);

        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<RefreshToken, Guid> _tokenRepo;
        private readonly IRepository<AuditLog, Guid> _auditRepo;

        public AccountSecurityAppService(IRepository<User, Guid> userRepo, IRepository<RefreshToken, Guid> tokenRepo,
            IRepository<AuditLog, Guid> auditRepo)
        {
            _userRepo = userRepo;
            _tokenRepo = tokenRepo;
            _auditRepo = auditRepo;
        }

        public async Task<BaseResponse<SecurityStatusDto>> GetStatusAsync(Guid userId, Guid sessionId)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user == null)
                return NotFound<SecurityStatusDto>("Không tìm thấy tài khoản.");
            var now = DateTime.UtcNow;
            return Success(new SecurityStatusDto
            {
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                PasswordChangedAt = Utc(user.PasswordChangedAt),
                LastLoginAt = Utc(user.LastLoginAt),
                GoogleLinked = user.AuthProvider == AuthProvider.Google,
                TwoFactorSupported = false,
                TwoFactorEnabled = false,
                ActiveSessions = await _tokenRepo.CountAsync(x => x.UserId == userId && !x.IsRevoked && x.ExpiryDate > now)
            });
        }

        public async Task<BaseResponse<bool>> ChangePasswordAsync(Guid userId, Guid sessionId, ChangePasswordCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var user = await _userRepo.FindAsync(userId);
            if (user == null || !user.IsActive)
                return NotFound<bool>("Không tìm thấy tài khoản.");
            if (string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest<bool>("Tài khoản đăng nhập bằng Google chưa có mật khẩu; hãy dùng Quên mật khẩu để tạo mật khẩu.");

            // Repeated wrong current passwords are counted from the audit trail (survives restarts, shared by all servers).
            var since = DateTime.UtcNow - FailedWindow;
            var failures = await _auditRepo.CountAsync(x => x.ActorId == userId && x.Module == "Auth" && x.Action == "ChangePassword" &&
                                                            x.Result == AuditResult.Failed && x.ErrorCode == "WrongCurrentPassword" &&
                                                            x.OccurredAt >= since);
            if (failures >= MaxFailedChanges)
                return new BaseResponse<bool>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.TooManyRequests,
                    Message = $"Bạn đã nhập sai mật khẩu hiện tại quá {MaxFailedChanges} lần. Vui lòng thử lại sau {FailedWindow.TotalMinutes:0} phút."
                };
            if (string.IsNullOrEmpty(command.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(command.CurrentPassword, user.PasswordHash))
            {
                await Audit.LogAsync(new AuditEntry("Auth", "ChangePassword", "User", user.Id.ToString(), user.Email)
                {
                    Result = AuditResult.Failed, ErrorCode = "WrongCurrentPassword"
                });
                await uow.CompleteAsync();
                return BadRequest<bool>("Mật khẩu hiện tại không đúng.");
            }
            var passwordError = AuthAppService.ValidatePassword(command.NewPassword);
            if (passwordError != null)
                return BadRequest<bool>(passwordError);
            if (command.NewPassword != command.ConfirmNewPassword)
                return BadRequest<bool>("Xác nhận mật khẩu mới không khớp.");
            if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.PasswordHash))
                return BadRequest<bool>("Mật khẩu mới phải khác mật khẩu hiện tại.");

            var now = DateTime.UtcNow;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
            user.PasswordChangedAt = now;
            user.UpdatedAt = now;
            await _userRepo.UpdateAsync(user, autoSave: true);
            var revoked = command.RevokeOtherSessions ? await RevokeAsync(userId, x => x.Id != sessionId) : 0;
            await Audit.LogAsync(new AuditEntry("Auth", "ChangePassword", "User", user.Id.ToString(), user.Email)
            {
                ChangedFields = ["password"], Details = new { revokedOtherSessions = revoked }
            });
            await uow.CompleteAsync();
            return Success(true, revoked > 0
                ? $"Đổi mật khẩu thành công. Đã đăng xuất {revoked} phiên khác."
                : "Đổi mật khẩu thành công.");
        }

        public async Task<BaseResponse<List<SessionDto>>> GetSessionsAsync(Guid userId, Guid sessionId)
        {
            var now = DateTime.UtcNow;
            var rows = await AsyncExecuter.ToListAsync((await _tokenRepo.GetQueryableAsync())
                .Where(x => x.UserId == userId && !x.IsRevoked && x.ExpiryDate > now)
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100));
            return Success(rows.Select(x => new SessionDto
            {
                Id = x.Id,
                StartedAt = Utc(x.StartedAt ?? x.CreatedAt)!.Value,
                LastActiveAt = Utc(x.CreatedAt)!.Value,
                ExpiresAt = Utc(x.ExpiryDate)!.Value,
                UserAgent = x.UserAgent,
                IsCurrent = x.Id == sessionId
            }).OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.LastActiveAt).ToList());
        }

        // Keeps the current session; the access tokens of revoked sessions stop working on the next request.
        public async Task<BaseResponse<RevokeSessionsResultDto>> RevokeOtherSessionsAsync(Guid userId, Guid sessionId)
        {
            var revoked = await RevokeAsync(userId, x => x.Id != sessionId);
            await Audit.LogAsync(new AuditEntry("Auth", "RevokeOtherSessions", "User", userId.ToString(), null)
            {
                Details = new { revoked }
            });
            return Success(new RevokeSessionsResultDto { Revoked = revoked },
                revoked == 0 ? "Không có phiên nào khác đang đăng nhập." : $"Đã đăng xuất {revoked} phiên khác.");
        }

        public async Task<BaseResponse<RevokeSessionsResultDto>> RevokeSessionAsync(Guid userId, Guid sessionId, Guid targetId)
        {
            var target = await _tokenRepo.FindAsync(targetId);
            if (target == null || target.UserId != userId)
                return NotFound<RevokeSessionsResultDto>("Không tìm thấy phiên đăng nhập.");
            var revoked = target.IsRevoked ? 0 : await RevokeAsync(userId, x => x.Id == targetId);
            if (revoked > 0)
                await Audit.LogAsync(new AuditEntry("Auth", "RevokeSession", "Session", targetId.ToString(), null)
                {
                    Details = new { current = targetId == sessionId }
                });
            return Success(new RevokeSessionsResultDto { Revoked = revoked },
                revoked == 0 ? "Phiên này đã kết thúc trước đó." : targetId == sessionId ? "Đã đăng xuất phiên hiện tại." : "Đã đăng xuất phiên đã chọn.");
        }

        private async Task<int> RevokeAsync(Guid userId, System.Linq.Expressions.Expression<Func<RefreshToken, bool>> which)
        {
            var tokens = await AsyncExecuter.ToListAsync((await _tokenRepo.GetQueryableAsync())
                .Where(x => x.UserId == userId && !x.IsRevoked).Where(which));
            foreach (var token in tokens)
                token.IsRevoked = true;
            if (tokens.Count > 0)
                await _tokenRepo.UpdateManyAsync(tokens, autoSave: true);
            return tokens.Count;
        }

        private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    }
}
