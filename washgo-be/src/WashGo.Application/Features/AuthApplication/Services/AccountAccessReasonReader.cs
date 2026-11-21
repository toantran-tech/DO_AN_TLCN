using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services;

/// <summary>
/// The reason shown to the account owner when sign-in or a trusted session is refused because the account is locked or
/// deleted. Deleted → the reason of the delete only (never an earlier lock reason); locked → the reason of the current lock
/// (legacy locks fall back to the Shipper/Merchant lock reason). No actor, no audit note. Call only after the caller proved
/// it owns the account (password, Google, a valid session or refresh token).
/// </summary>
public class AccountAccessReasonReader(IRepository<Shipper, Guid> shippers, IRepository<Merchant, Guid> merchants) : ITransientDependency
{
    public async Task<string?> ReasonAsync(User user)
    {
        if (user.IsDeleted) return Clean(user.DeleteReason);
        if (user.IsActive) return null;
        if (Clean(user.StatusReason) is { } reason) return reason;
        // Locks made before the account kept its own reason.
        if (await shippers.FirstOrDefaultAsync(x => x.UserId == user.Id) is { LockedAt: not null } shipper)
            return Clean(shipper.LockReason);
        return await merchants.FindAsync(user.Id) is { } merchant && merchant.Status != MerchantStatus.Active
            ? Clean(merchant.RejectionReason)
            : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
