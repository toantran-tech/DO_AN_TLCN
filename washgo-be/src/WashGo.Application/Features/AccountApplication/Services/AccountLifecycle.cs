using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Domain.Entities.Identity;

namespace WashGo.Application.Features.AccountApplication.Services;

/// <summary>
/// Khóa/mở/xóa mềm ở mức tài khoản đăng nhập, dùng chung cho trang tài khoản, trang Merchant và trang Shipper.
/// Không đổi trạng thái kinh doanh của Merchant hay trạng thái nhận việc của Shipper. Gọi trong UoW có transaction:
/// thu hồi phiên/challenge cùng transaction, ngắt kết nối thời gian thực sau commit.
/// </summary>
public class AccountLifecycle(IRepository<RefreshToken, Guid> tokens, IRepository<OtpCode, Guid> otps,
    IRealtimeNotifier realtime, IUnitOfWorkManager units) : ITransientDependency
{
    /// <summary>IsActive = false, ghi người/lý do/giờ server, thu hồi mọi phiên và ngắt kết nối thời gian thực.</summary>
    public async Task LockAsync(User user, Guid? actorId, string? reason)
    {
        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.StatusChangedAt = now;
        user.StatusChangedBy = actorId;
        user.StatusReason = reason;
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id);
        DisconnectAfterCommit(user.Id);
    }

    /// <summary>Mở đăng nhập; phiên cũ không sống lại (đã bị thu hồi lúc khóa), người dùng phải đăng nhập mới.</summary>
    public void Unlock(User user, Guid? actorId, string? reason)
    {
        if (user.IsDeleted) throw new InvalidOperationException("A soft-deleted account cannot be unlocked.");
        var now = DateTime.UtcNow;
        user.IsActive = true;
        user.StatusChangedAt = now;
        user.StatusChangedBy = actorId;
        user.StatusReason = reason;
        user.UpdatedAt = now;
    }

    /// <summary>
    /// Xóa mềm: giữ nguyên User.Id và mọi khóa ngoại; email/Google được giải phóng cho tài khoản mới (unique có điều kiện).
    /// Không ghi đè thời điểm xóa nếu tài khoản đã xóa trước đó.
    /// </summary>
    public async Task SoftDeleteAsync(User user, Guid? actorId, string reason)
    {
        if (user.IsDeleted) return;
        var now = DateTime.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.DeletedBy = actorId;
        user.DeleteReason = reason;
        user.IsActive = false;
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id);
        await InvalidateChallengesAsync(user);
        DisconnectAfterCommit(user.Id);
    }

    public async Task<int> RevokeSessionsAsync(Guid userId)
    {
        var active = await tokens.GetListAsync(x => x.UserId == userId && !x.IsRevoked);
        foreach (var token in active)
            token.IsRevoked = true;
        if (active.Count > 0)
            await tokens.UpdateManyAsync(active, autoSave: true);
        return active.Count;
    }

    /// <summary>OTP đăng ký/quên mật khẩu/đổi email còn hiệu lực của tài khoản (hoặc email của nó) không dùng lại được.</summary>
    public async Task InvalidateChallengesAsync(User user)
    {
        var email = user.Email.Trim().ToLowerInvariant();
        var open = await otps.GetListAsync(x => !x.IsUsed && (x.UserId == user.Id || x.Email.ToLower() == email));
        var now = DateTime.UtcNow;
        foreach (var otp in open)
        {
            otp.IsUsed = true;
            otp.UsedAt = now;
        }
        if (open.Count > 0)
            await otps.UpdateManyAsync(open, autoSave: true);
    }

    /// <summary>JWT chỉ được kiểm khi mở kết nối SignalR, nên socket đang mở bị đóng sau khi khóa/xóa đã commit.</summary>
    public void DisconnectAfterCommit(Guid userId)
    {
        if (units.Current is { } uow)
            uow.OnCompleted(async () => await realtime.DisconnectUserAsync(userId));
        else
            _ = realtime.DisconnectUserAsync(userId);
    }
}
