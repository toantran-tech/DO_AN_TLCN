using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Application.Features.AccountApplication.Services;

/// <summary>
/// Quyền quản lý tài khoản. Admin đang hoạt động có sẵn quyền thao tác tài khoản thông thường; quyền quản lý Admin,
/// đọc tài khoản đã xóa và khôi phục được cấp riêng. "Admin quản trị thiết yếu" = Admin chưa xóa, đang hoạt động,
/// có Accounts.ManageAdmins; hệ thống luôn giữ ít nhất một người như vậy.
/// </summary>
public class AccountPermissionService(IRepository<UserPermission, Guid> grants, IRepository<User, Guid> users,
    IRepository<Role, Guid> roles, IWashOrderRepository locks, IAsyncQueryableExecuter executer) : ITransientDependency
{
    /// <summary>Serialises every change that can remove a privileged Admin (lock, delete, permission change).</summary>
    public Task LockAdminGuardAsync() => locks.AcquireLockAsync("account-admin-guard");

    public async Task<HashSet<string>> GrantsAsync(Guid userId) =>
        (await executer.ToListAsync((await grants.GetQueryableAsync()).Where(x => x.UserId == userId).Select(x => x.Permission)))
        .ToHashSet(StringComparer.Ordinal);

    public async Task<Dictionary<Guid, List<string>>> GrantsAsync(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0) return [];
        var rows = await executer.ToListAsync((await grants.GetQueryableAsync()).Where(x => userIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.Permission }));
        return rows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Permission).OrderBy(x => x).ToList());
    }

    /// <summary>Ids of active, not deleted Admins holding Accounts.ManageAdmins.</summary>
    public async Task<List<Guid>> PrivilegedAdminIdsAsync()
    {
        var query = from grant in await grants.GetQueryableAsync()
                    join user in await users.GetQueryableAsync() on grant.UserId equals user.Id
                    join role in await roles.GetQueryableAsync() on user.RoleId equals role.Id
                    where grant.Permission == AccountPermissions.ManageAdmins && role.Name == "Admin" && user.IsActive && !user.IsDeleted
                    select user.Id;
        return await executer.ToListAsync(query.Distinct());
    }

    /// <summary>Adds/removes grant rows so the user holds exactly <paramref name="target"/>; returns (added, removed).</summary>
    public async Task<(List<string> Added, List<string> Removed)> SetAsync(Guid userId, IReadOnlyCollection<string> target, Guid? actorId)
    {
        var current = await grants.GetListAsync(x => x.UserId == userId);
        var removed = current.Where(x => !target.Contains(x.Permission)).ToList();
        var added = target.Where(p => current.All(x => x.Permission != p)).ToList();
        if (removed.Count > 0) await grants.DeleteManyAsync(removed, autoSave: true);
        foreach (var permission in added)
            await grants.InsertAsync(new UserPermission(Guid.NewGuid())
            {
                UserId = userId, Permission = permission, GrantedAt = DateTime.UtcNow, GrantedBy = actorId
            }, autoSave: true);
        return (added, removed.Select(x => x.Permission).ToList());
    }
}
