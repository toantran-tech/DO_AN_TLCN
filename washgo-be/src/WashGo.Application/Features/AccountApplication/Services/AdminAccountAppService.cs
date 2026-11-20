using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.AccountContracts.Commands;
using WashGo.Application.Contracts.Features.AccountContracts.Queries;
using WashGo.Application.Contracts.Features.AccountContracts.Results;
using WashGo.Application.Contracts.Features.AccountContracts.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Contracts.Features.MerchantContracts.Services;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Features.AuthApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.MerchantApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Identity.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;
using PasswordHasher = global::BCrypt.Net.BCrypt;

namespace WashGo.Application.Features.AccountApplication.Services;

/// <summary>
/// Trang Admin quản lý tài khoản. Mọi quyết định (quyền, tự thao tác, tài khoản được bảo vệ, Admin quản trị cuối cùng,
/// revision, nghĩa vụ còn mở) được kiểm tra lại trên BE trong transaction của từng tài khoản; FE chỉ hiển thị capability.
/// Không xóa cứng, không đổi role, không cascade dữ liệu nghiệp vụ.
/// </summary>
public class AdminAccountAppService(
    IRepository<User, Guid> users,
    IRepository<Role, Guid> roles,
    IRepository<Customer, Guid> customers,
    IRepository<Merchant, Guid> merchants,
    IShipperRepository shippers,
    IRepository<ShipperAudit, Guid> shipperAudits,
    IRepository<RefreshToken, Guid> tokens,
    IOtpCodeRepository otps,
    IRepository<AuditLog, Guid> auditLogs,
    AccountPermissionService permissions,
    AccountLifecycle lifecycle,
    AccountObligationReader obligations,
    AccountIdentityGuard identity,
    IAuthEmailSender emailSender,
    IDataFilter dataFilter,
    ILogger<AdminAccountAppService> logger) : WashGoAppService, IAdminAccountAppService
{
    private const string Module = "Account";
    private const int MaxBulk = 100;
    private static readonly string[] BulkActions = ["Lock", "Unlock", "SoftDelete"];

    // ───────────────────────────── caller / target ─────────────────────────────

    private sealed record Caller(Guid Id, string Name, HashSet<string> Grants, DateTime? ReauthenticatedAt)
    {
        public bool Has(string permission) => AccountPermissions.AdminDefaults.Contains(permission) || Grants.Contains(permission);
        public bool Reauthenticated => ReauthenticatedAt.HasValue &&
                                       ReauthenticatedAt.Value >= DateTime.UtcNow - AccountSecurityAppService.ReauthenticationWindow;
    }

    /// <summary>What the rules need to know about an account (works for list rows and loaded entities).</summary>
    private sealed record Subject(Guid Id, string Role, bool IsActive, bool IsDeleted, bool IsProtected, MerchantStatus? MerchantStatus,
        bool HasShipperProfile, bool ShipperDeleted);

    private sealed class Target
    {
        public required User User { get; init; }
        public required string Role { get; init; }
        public Merchant? Merchant { get; set; }
        public Shipper? Shipper { get; set; }
        public Subject Subject => new(User.Id, Role, User.IsActive, User.IsDeleted, User.IsProtected, Merchant?.Status,
            Shipper != null, Shipper?.IsDeleted ?? false);
    }

    private sealed record Denial(HttpStatusCode Status, string Code, string Message);

    /// <summary>Re-read on every write: a caller locked, deleted or stripped of a grant meanwhile is refused.</summary>
    private async Task<Caller?> CallerAsync()
    {
        if (!CurrentUser.Id.HasValue || !CurrentUser.IsInRole("Admin")) return null;
        var me = await users.FindAsync(CurrentUser.Id.Value);
        if (me == null || me.IsDeleted || !me.IsActive) return null;
        var role = await roles.FindAsync(me.RoleId);
        if (role?.Name != "Admin") return null;
        DateTime? reauthenticatedAt = null;
        if (Guid.TryParse(CurrentUser.FindClaim("session_id")?.Value, out var sessionId) &&
            await tokens.FindAsync(sessionId) is { IsRevoked: false } session && session.UserId == me.Id)
            reauthenticatedAt = session.ReauthenticatedAt;
        return new Caller(me.Id, me.FullName, await permissions.GrantsAsync(me.Id), reauthenticatedAt);
    }

    private async Task<Target?> TargetAsync(Guid id)
    {
        var user = await users.FindAsync(id);
        if (user == null) return null;
        var role = (await roles.FindAsync(user.RoleId))?.Name ?? "Customer";
        var target = new Target { User = user, Role = role };
        if (role == "Merchant") target.Merchant = await merchants.FindAsync(id);
        if (role == "Shipper")
            using (dataFilter.Disable<ISoftDelete>())
                target.Shipper = await shippers.FirstOrDefaultAsync(x => x.UserId == id);
        return target;
    }

    private static string RuleAction(string action) => action switch
    {
        "Update" => "Edit",
        "EmailChange" => "ChangeEmail",
        "SoftDelete" => "Delete",
        "Permissions" => "ManagePermissions",
        _ => action
    };

    /// <summary>Permission/state rules shared by capabilities, preview and execution. Business obligations are checked separately.</summary>
    private static Denial? Rule(Caller caller, Subject t, string action, IReadOnlyCollection<Guid> privileged)
    {
        var permission = action switch
        {
            "Edit" or "ChangeEmail" => AccountPermissions.Update,
            "Lock" or "Unlock" => AccountPermissions.SetStatus,
            "Delete" => AccountPermissions.SoftDelete,
            "Restore" => AccountPermissions.Restore,
            "ManagePermissions" => AccountPermissions.ManageAdmins,
            _ => AccountPermissions.Read
        };
        if (!caller.Has(permission))
            return new(HttpStatusCode.Forbidden, "PermissionRequired", $"Bạn chưa được cấp quyền {permission}.");
        if (t.Role == "Admin" && !caller.Has(AccountPermissions.ManageAdmins))
            return new(HttpStatusCode.Forbidden, "ManageAdminsRequired", "Thao tác với tài khoản Admin cần quyền Accounts.ManageAdmins.");
        if (t.IsProtected)
            return new(HttpStatusCode.Conflict, "ProtectedAccount", "Tài khoản hệ thống được bảo vệ, không thao tác qua trang quản lý tài khoản.");
        var self = new Denial(HttpStatusCode.Conflict, "CannotModifySelf", "Không thể tự khóa, tự xóa hoặc tự đổi quyền của chính mình.");
        var deleted = new Denial(HttpStatusCode.Conflict, "AccountAlreadyDeleted", "Tài khoản đã bị xóa.");
        var last = new Denial(HttpStatusCode.Conflict, "LastPrivilegedAdmin",
            "Đây là Admin hoạt động cuối cùng có quyền quản trị tài khoản Admin; hãy cấp quyền cho Admin khác trước.");
        var isLastPrivileged = privileged.Contains(t.Id) && privileged.All(x => x == t.Id);
        switch (action)
        {
            case "Edit" or "ChangeEmail":
                if (t.IsDeleted) return deleted;
                break;
            case "Lock":
                if (t.Id == caller.Id) return self;
                if (t.IsDeleted) return deleted;
                if (isLastPrivileged) return last;
                break;
            case "Unlock":
                if (t.IsDeleted) return deleted;
                if (t.Role == "Merchant" && t.MerchantStatus != MerchantStatus.Active)
                    return new(HttpStatusCode.Conflict, "PartnerNotActive",
                        "Cửa hàng đang tạm ngưng/ngừng kinh doanh; mở lại tại trang Merchant (mở tài khoản không tự duyệt Merchant).");
                if (t.Role == "Shipper" && (!t.HasShipperProfile || t.ShipperDeleted))
                    return new(HttpStatusCode.Conflict, "PartnerProfileDeleted", "Hồ sơ Shipper đã bị xóa; không thể mở tài khoản.");
                break;
            case "Delete":
                if (t.Id == caller.Id) return self;
                if (isLastPrivileged) return last;
                break;
            case "Restore":
                if (!t.IsDeleted) return new(HttpStatusCode.Conflict, "AccountNotDeleted", "Tài khoản chưa bị xóa.");
                if (t.Role is "Merchant" or "Shipper")
                    return new(HttpStatusCode.Conflict, "RestoreNotSupportedForPartner",
                        "Chưa hỗ trợ khôi phục tài khoản Merchant/Shipper (hồ sơ đối tác cần quy trình riêng).");
                break;
            case "ManagePermissions":
                if (t.Role != "Admin") return new(HttpStatusCode.BadRequest, "NotAdminAccount", "Chỉ cấp quyền quản lý tài khoản cho tài khoản Admin.");
                if (t.Id == caller.Id) return self;
                if (t.IsDeleted) return deleted;
                break;
        }
        return null;
    }

    private static Denial Reauth() => new(HttpStatusCode.Forbidden, "ReauthenticationRequired",
        "Thao tác với tài khoản Admin cần xác thực lại: gọi POST /auth/reauthenticate rồi thử lại.");

    private AccountCapabilitiesDto Capabilities(Caller caller, Subject t, IReadOnlyCollection<Guid> privileged, AccountObligationsDto? owed)
    {
        var caps = new AccountCapabilitiesDto { RequiresReauthentication = t.Role == "Admin" };
        bool Allow(string action, bool applicable)
        {
            if (!applicable) return false;
            var denial = Rule(caller, t, action, privileged);
            if (denial == null) return true;
            caps.Blocked.Add(new AccountBlockDto { Action = action, Code = denial.Code, Message = denial.Message });
            return false;
        }
        void Block(string action, string code, string message) =>
            caps.Blocked.Add(new AccountBlockDto { Action = action, Code = code, Message = message });

        caps.CanEdit = Allow("Edit", !t.IsDeleted);
        caps.CanChangeEmail = Allow("ChangeEmail", !t.IsDeleted);
        caps.CanLock = Allow("Lock", t.IsActive && !t.IsDeleted);
        if (caps.CanLock && owed is { ActiveTasks: > 0 } && t.Role == "Shipper")
        {
            caps.CanLock = false;
            Block("Lock", "HasActiveTasks", $"Shipper còn {owed.ActiveTasks} nhiệm vụ chưa hoàn tất; cần bàn giao trước khi khóa.");
        }
        caps.CanUnlock = Allow("Unlock", !t.IsActive && !t.IsDeleted);
        caps.CanDelete = Allow("Delete", !t.IsDeleted);
        if (caps.CanDelete && owed is { DeleteBlockers.Count: > 0 })
        {
            caps.CanDelete = false;
            Block("Delete", "AccountHasOpenObligations", "Tài khoản còn nghĩa vụ chưa xử lý: " + string.Join(", ", owed.DeleteBlockers) + ".");
        }
        caps.CanRestore = Allow("Restore", t.IsDeleted);
        caps.CanManagePermissions = Allow("ManagePermissions", t.Role == "Admin" && !t.IsDeleted);
        return caps;
    }

    // ───────────────────────────── responses ─────────────────────────────

    private static BaseResponse<T> Fail<T>(HttpStatusCode status, string code, string message, T? data = default,
        Dictionary<string, string[]>? errors = null, object? meta = null) => new()
    {
        Succeeded = false, StatusCode = status, Message = message, Data = data, Errors = errors ?? [],
        Meta = meta ?? new { code, fields = errors?.Keys.ToArray() }
    };

    private static BaseResponse<T> Invalid<T>(Dictionary<string, string[]> errors) =>
        Fail<T>(HttpStatusCode.BadRequest, "ValidationFailed", errors.First().Value[0], default, errors);

    private static BaseResponse<T> NotAdmin<T>() =>
        Fail<T>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin đang hoạt động được quản lý tài khoản.");

    private static BaseResponse<T> Missing<T>() =>
        Fail<T>(HttpStatusCode.NotFound, "NotFound", "Không tìm thấy tài khoản.");

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;

    private static string SignIn(bool hasPassword, bool google) =>
        hasPassword && google ? "PasswordAndGoogle" : google ? "Google" : "Password";

    /// <summary>Records a refused action (permission, self, protected, last Admin, re-authentication) and returns it.</summary>
    private async Task<BaseResponse<AccountDetailDto>> DenyAsync(IUnitOfWork? uow, Target target, string action, Denial denial)
    {
        await Audit.LogAsync(new AuditEntry(Module, action, "User", target.User.Id.ToString(), target.User.FullName)
        {
            Result = denial.Status == HttpStatusCode.Forbidden ? AuditResult.Denied : AuditResult.Failed, ErrorCode = denial.Code,
            Details = new { role = target.Role }
        });
        if (uow != null) await uow.CompleteAsync();
        return Fail<AccountDetailDto>(denial.Status, denial.Code, denial.Message);
    }

    private async Task<AccountDetailDto> DetailAsync(Caller caller, Target target)
    {
        var u = target.User;
        var privileged = target.Role == "Admin" ? await permissions.PrivilegedAdminIdsAsync() : new List<Guid>();
        var owed = u.IsDeleted ? new AccountObligationsDto() : await obligations.ReadAsync(u.Id, target.Role, target.Shipper?.Id);
        var actorIds = new[] { u.StatusChangedBy, u.DeletedBy }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var actors = actorIds.Count == 0 ? new Dictionary<Guid, string>() : (await users.GetListAsync(x => actorIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.FullName);
        AccountActorDto? Actor(Guid? id) => id.HasValue ? new AccountActorDto { Id = id.Value, FullName = actors.GetValueOrDefault(id.Value) } : null;
        var dto = new AccountDetailDto
        {
            Id = u.Id, Revision = u.Revision, AvatarUrl = string.IsNullOrWhiteSpace(u.Avatar) ? null : u.Avatar,
            FullName = u.FullName, Email = u.Email, Phone = u.Phone, Role = target.Role, IsActive = u.IsActive,
            IsDeleted = u.IsDeleted, DeletedAt = Utc(u.DeletedAt), IsProtected = u.IsProtected,
            CreatedAt = Utc(u.CreatedAt), UpdatedAt = Utc(u.UpdatedAt), LastLoginAt = Utc(u.LastLoginAt),
            HasPassword = !string.IsNullOrEmpty(u.PasswordHash), GoogleLinked = u.AuthProvider == AuthProvider.Google && u.ProviderId != null,
            StatusChangedAt = Utc(u.StatusChangedAt), StatusChangedBy = Actor(u.StatusChangedBy), StatusReason = u.StatusReason,
            DeletedBy = Actor(u.DeletedBy), DeleteReason = u.DeleteReason,
            CredentialDelivery = u.CredentialDelivery, CredentialSentAt = Utc(u.CredentialSentAt),
            Obligations = owed
        };
        dto.SignInMethod = SignIn(dto.HasPassword, dto.GoogleLinked);
        if (target.Role == "Admin")
            dto.Permissions = (await permissions.GrantsAsync(u.Id)).OrderBy(x => x).ToList();
        if (target.Merchant != null)
            dto.Partner = new AccountPartnerDto
            {
                MerchantId = target.Merchant.Id, BusinessName = target.Merchant.BusinessName, MerchantStatus = target.Merchant.Status,
                CredentialDelivery = target.Merchant.CredentialDelivery, CredentialSentAt = Utc(target.Merchant.CredentialSentAt)
            };
        if (target.Shipper != null)
            dto.Partner = new AccountPartnerDto
            {
                ShipperId = target.Shipper.Id, ShipperMerchantId = target.Shipper.MerchantId, ShipperDeleted = target.Shipper.IsDeleted,
                ShipperMerchantName = (await merchants.FindAsync(target.Shipper.MerchantId))?.BusinessName,
                CredentialDelivery = target.Shipper.CredentialDelivery, CredentialSentAt = Utc(target.Shipper.CredentialSentAt)
            };
        dto.Capabilities = Capabilities(caller, target.Subject, privileged, u.IsDeleted ? null : owed);
        return dto;
    }

    // ───────────────────────────── reads ─────────────────────────────

    private sealed class Row
    {
        public Guid Id { get; set; }
        public long Revision { get; set; }
        public string? Avatar { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsProtected { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool HasPassword { get; set; }
        public bool GoogleLinked { get; set; }
        public Guid? MerchantId { get; set; }
        public string? BusinessName { get; set; }
        public MerchantStatus? MerchantStatus { get; set; }
        public CredentialDeliveryStatus? MerchantCredential { get; set; }
        public DateTime? MerchantCredentialSentAt { get; set; }
        public Guid? ShipperId { get; set; }
        public Guid? ShipperMerchantId { get; set; }
        public bool? ShipperDeleted { get; set; }
        public CredentialDeliveryStatus? ShipperCredential { get; set; }
        public DateTime? ShipperCredentialSentAt { get; set; }
    }

    private async Task<IQueryable<Row>> RowsAsync()
    {
        var shipperQuery = await shippers.GetQueryableAsync();
        return from u in await users.GetQueryableAsync()
               join r in await roles.GetQueryableAsync() on u.RoleId equals r.Id
               join m in await merchants.GetQueryableAsync() on u.Id equals m.Id into mj
               from m in mj.DefaultIfEmpty()
               join s in shipperQuery on u.Id equals s.UserId into sj
               from s in sj.DefaultIfEmpty()
               select new Row
               {
                   Id = u.Id, Revision = u.Revision, Avatar = u.Avatar, FullName = u.FullName, Email = u.Email, Phone = u.Phone,
                   Role = r.Name, IsActive = u.IsActive, IsDeleted = u.IsDeleted, DeletedAt = u.DeletedAt, IsProtected = u.IsProtected,
                   CreatedAt = u.CreatedAt, UpdatedAt = u.UpdatedAt, LastLoginAt = u.LastLoginAt,
                   HasPassword = u.PasswordHash != null, GoogleLinked = u.AuthProvider == AuthProvider.Google && u.ProviderId != null,
                   MerchantId = m == null ? (Guid?)null : m.Id, BusinessName = m == null ? null : m.BusinessName,
                   MerchantStatus = m == null ? (MerchantStatus?)null : m.Status,
                   MerchantCredential = m == null ? (CredentialDeliveryStatus?)null : m.CredentialDelivery,
                   MerchantCredentialSentAt = m == null ? null : m.CredentialSentAt,
                   ShipperId = s == null ? (Guid?)null : s.Id, ShipperMerchantId = s == null ? (Guid?)null : s.MerchantId,
                   ShipperDeleted = s == null ? (bool?)null : s.IsDeleted,
                   ShipperCredential = s == null ? (CredentialDeliveryStatus?)null : s.CredentialDelivery,
                   ShipperCredentialSentAt = s == null ? null : s.CredentialSentAt
               };
    }

    public async Task<BaseResponse<AccountPageDto>> GetListAsync(AccountListQuery query)
    {
        var caller = await CallerAsync();
        if (caller == null) return NotAdmin<AccountPageDto>();
        var errors = new Dictionary<string, string[]>();
        var deletedMode = (query.Deleted ?? "Exclude").Trim().ToLowerInvariant();
        if (deletedMode is not ("exclude" or "include" or "only")) errors["deleted"] = ["deleted chỉ nhận Exclude, Include hoặc Only."];
        string? role = null;
        if (!string.IsNullOrWhiteSpace(query.Role) && (role = AccountRules.CanonicalRole(query.Role)) == null)
            errors["role"] = ["role chỉ nhận Customer, Merchant, Shipper hoặc Admin."];
        var sortBy = (query.SortBy ?? "createdAt").Trim().ToLowerInvariant();
        if (sortBy is not ("createdat" or "lastloginat" or "fullname" or "deletedat") || sortBy == "deletedat" && deletedMode == "exclude")
            errors["sortBy"] = ["sortBy chỉ nhận createdAt, lastLoginAt, fullName (deletedAt khi xem tài khoản đã xóa)."];
        var direction = (query.SortDirection ?? (sortBy == "fullname" ? "asc" : "desc")).Trim().ToLowerInvariant();
        if (direction is not ("asc" or "desc")) errors["sortDirection"] = ["sortDirection chỉ nhận asc hoặc desc."];
        if (query.CreatedFrom > 0 && query.CreatedTo > 0 && query.CreatedFrom > query.CreatedTo)
            errors["createdFrom"] = ["createdFrom phải nhỏ hơn hoặc bằng createdTo."];
        if (query.Take is < 1 or > 100) errors["take"] = ["take từ 1 đến 100."];
        if (errors.Count > 0) return Invalid<AccountPageDto>(errors);
        if (deletedMode != "exclude" && !caller.Has(AccountPermissions.ReadDeleted))
            return Fail<AccountPageDto>(HttpStatusCode.Forbidden, "PermissionRequired", "Xem tài khoản đã xóa cần quyền Accounts.ReadDeleted.");

        var page = Math.Max(1, query.Page ?? 1);
        var take = query.Take ?? 20;
        // Shipper rows of deleted accounts are soft-deleted too; read them so partner data stays visible.
        using var deletedShippers = dataFilter.Disable<ISoftDelete>();
        var rows = await RowsAsync();
        rows = deletedMode switch { "only" => rows.Where(x => x.IsDeleted), "include" => rows, _ => rows.Where(x => !x.IsDeleted) };
        if (role != null) rows = rows.Where(x => x.Role == role);
        if (query.IsActive.HasValue) rows = rows.Where(x => x.IsActive == query.IsActive.Value);
        if (query.CreatedFrom > 0)
        {
            var from = DateTime.UnixEpoch.AddSeconds(query.CreatedFrom.Value);
            rows = rows.Where(x => x.CreatedAt >= from);
        }
        if (query.CreatedTo > 0)
        {
            var to = DateTime.UnixEpoch.AddSeconds(query.CreatedTo.Value + 1);
            rows = rows.Where(x => x.CreatedAt < to);
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            rows = rows.Where(x => x.FullName.ToLower().Contains(keyword) || x.Email.ToLower().Contains(keyword) ||
                                   x.Phone != null && x.Phone.Contains(keyword) ||
                                   x.BusinessName != null && x.BusinessName.ToLower().Contains(keyword));
        }
        var total = await AsyncExecuter.LongCountAsync(rows);
        var desc = direction == "desc";
        // Stable order: id breaks ties; a missing last login/deletion is listed last in both directions, never given a fake date.
        IOrderedQueryable<Row> ordered = sortBy switch
        {
            "lastloginat" => desc
                ? rows.OrderBy(x => x.LastLoginAt == null).ThenByDescending(x => x.LastLoginAt)
                : rows.OrderBy(x => x.LastLoginAt == null).ThenBy(x => x.LastLoginAt),
            "deletedat" => desc
                ? rows.OrderBy(x => x.DeletedAt == null).ThenByDescending(x => x.DeletedAt)
                : rows.OrderBy(x => x.DeletedAt == null).ThenBy(x => x.DeletedAt),
            "fullname" => desc ? rows.OrderByDescending(x => x.FullName) : rows.OrderBy(x => x.FullName),
            _ => desc ? rows.OrderByDescending(x => x.CreatedAt) : rows.OrderBy(x => x.CreatedAt)
        };
        var items = await AsyncExecuter.ToListAsync((desc ? ordered.ThenByDescending(x => x.Id) : ordered.ThenBy(x => x.Id))
            .Skip((page - 1) * take).Take(take));

        var merchantIds = items.Where(x => x.ShipperMerchantId.HasValue).Select(x => x.ShipperMerchantId!.Value).Distinct().ToList();
        var merchantNames = merchantIds.Count == 0 ? new Dictionary<Guid, string>() : (await merchants.GetListAsync(x => merchantIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.BusinessName);
        var privileged = items.Any(x => x.Role == "Admin") ? await permissions.PrivilegedAdminIdsAsync() : new List<Guid>();
        var list = items.Select(x =>
        {
            var dto = new AccountListItemDto
            {
                Id = x.Id, Revision = x.Revision, AvatarUrl = string.IsNullOrWhiteSpace(x.Avatar) ? null : x.Avatar,
                FullName = x.FullName, Email = x.Email, Phone = x.Phone, Role = x.Role, IsActive = x.IsActive, IsDeleted = x.IsDeleted,
                DeletedAt = Utc(x.DeletedAt), IsProtected = x.IsProtected, CreatedAt = Utc(x.CreatedAt), UpdatedAt = Utc(x.UpdatedAt),
                LastLoginAt = Utc(x.LastLoginAt), HasPassword = x.HasPassword, GoogleLinked = x.GoogleLinked,
                SignInMethod = SignIn(x.HasPassword, x.GoogleLinked)
            };
            if (x.MerchantId.HasValue && x.Role == "Merchant")
                dto.Partner = new AccountPartnerDto
                {
                    MerchantId = x.MerchantId, BusinessName = x.BusinessName, MerchantStatus = x.MerchantStatus,
                    CredentialDelivery = x.MerchantCredential, CredentialSentAt = Utc(x.MerchantCredentialSentAt)
                };
            if (x.ShipperId.HasValue)
                dto.Partner = new AccountPartnerDto
                {
                    ShipperId = x.ShipperId, ShipperMerchantId = x.ShipperMerchantId, ShipperDeleted = x.ShipperDeleted,
                    ShipperMerchantName = x.ShipperMerchantId.HasValue ? merchantNames.GetValueOrDefault(x.ShipperMerchantId.Value) : null,
                    CredentialDelivery = x.ShipperCredential, CredentialSentAt = Utc(x.ShipperCredentialSentAt)
                };
            dto.Capabilities = Capabilities(caller, new Subject(x.Id, x.Role, x.IsActive, x.IsDeleted, x.IsProtected,
                x.Role == "Merchant" ? x.MerchantStatus : null, x.ShipperId.HasValue, x.ShipperDeleted ?? false), privileged, null);
            return dto;
        }).ToList();
        return Success(new AccountPageDto { Items = list, Total = total, Page = page, Take = take, HasMore = (long)page * take < total },
            total == 0 ? "Không có tài khoản phù hợp." : null);
    }

    public async Task<BaseResponse<AccountSummaryDto>> GetSummaryAsync()
    {
        var caller = await CallerAsync();
        if (caller == null) return NotAdmin<AccountSummaryDto>();
        var canSeeDeleted = caller.Has(AccountPermissions.ReadDeleted);
        // One grouped query: every number comes from the same snapshot and the same scope as the list.
        var groups = await AsyncExecuter.ToListAsync(
            (from u in await users.GetQueryableAsync()
             join r in await roles.GetQueryableAsync() on u.RoleId equals r.Id
             select new { Role = r.Name, u.IsActive, u.IsDeleted })
            .GroupBy(x => new { x.Role, x.IsActive, x.IsDeleted })
            .Select(g => new { g.Key.Role, g.Key.IsActive, g.Key.IsDeleted, Count = g.Count() }));
        var result = new AccountSummaryDto { GeneratedAt = DateTime.UtcNow };
        foreach (var name in AccountRules.Roles)
        {
            var mine = groups.Where(x => x.Role == name).ToList();
            result.ByRole[name] = new AccountRoleCountDto
            {
                Total = mine.Where(x => !x.IsDeleted).Sum(x => x.Count),
                Active = mine.Where(x => !x.IsDeleted && x.IsActive).Sum(x => x.Count),
                Locked = mine.Where(x => !x.IsDeleted && !x.IsActive).Sum(x => x.Count),
                Deleted = canSeeDeleted ? mine.Where(x => x.IsDeleted).Sum(x => x.Count) : null
            };
        }
        result.Total = groups.Where(x => !x.IsDeleted).Sum(x => x.Count);
        result.Active = groups.Where(x => !x.IsDeleted && x.IsActive).Sum(x => x.Count);
        result.Locked = groups.Where(x => !x.IsDeleted && !x.IsActive).Sum(x => x.Count);
        result.Deleted = canSeeDeleted ? groups.Where(x => x.IsDeleted).Sum(x => x.Count) : null;
        return Success(result);
    }

    public async Task<BaseResponse<AccountDetailDto>> GetAsync(Guid id)
    {
        var caller = await CallerAsync();
        if (caller == null) return NotAdmin<AccountDetailDto>();
        var target = await TargetAsync(id);
        if (target == null || target.User.IsDeleted && !caller.Has(AccountPermissions.ReadDeleted))
            return Missing<AccountDetailDto>();
        return Success(await DetailAsync(caller, target));
    }

    public async Task<PaginatedResult<AuditLogDto>> GetActivitiesAsync(Guid id, AccountActivityQuery query)
    {
        var caller = await CallerAsync();
        var target = caller == null ? null : await TargetAsync(id);
        if (caller == null || target == null || target.User.IsDeleted && !caller.Has(AccountPermissions.ReadDeleted))
            return new PaginatedResult<AuditLogDto>
            {
                Succeeded = false, StatusCode = caller == null ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound,
                Message = caller == null ? "Chỉ Admin đang hoạt động được xem nhật ký tài khoản." : "Không tìm thấy tài khoản."
            };
        var page = Math.Max(1, query.Page ?? 1);
        var take = Math.Clamp(query.Take ?? 20, 1, 100);
        var key = id.ToString();
        var shipperKey = target.Shipper?.Id.ToString();
        // Events about this account (account page, sign-in, Merchant page — same id) and about its Shipper profile.
        var rows = (await auditLogs.GetQueryableAsync()).Where(x => x.ResourceId == key || shipperKey != null && x.ResourceId == shipperKey);
        var total = await AsyncExecuter.LongCountAsync(rows);
        var items = await AsyncExecuter.ToListAsync(rows.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * take).Take(take));
        return Success(items.Select(AuditLogAppService.ToDto), total, page, take, total == 0 ? "Chưa có hoạt động nào." : null);
    }

    // ───────────────────────────── create ─────────────────────────────

    public async Task<BaseResponse<AccountDetailDto>> CreateAsync(CreateAccountCommand command)
    {
        var caller = await CallerAsync();
        if (caller == null) return NotAdmin<AccountDetailDto>();
        var role = AccountRules.CanonicalRole(command.Role);
        if (role == null)
            return Invalid<AccountDetailDto>(new() { ["role"] = ["role chỉ nhận Customer, Merchant, Shipper hoặc Admin."] });
        if (!caller.Has(AccountPermissions.Create))
            return Fail<AccountDetailDto>(HttpStatusCode.Forbidden, "PermissionRequired", "Bạn chưa được cấp quyền Accounts.Create.");
        if (role == "Admin")
        {
            if (!caller.Has(AccountPermissions.ManageAdmins))
                return Fail<AccountDetailDto>(HttpStatusCode.Forbidden, "ManageAdminsRequired", "Tạo tài khoản Admin cần quyền Accounts.ManageAdmins.");
            if (!caller.Reauthenticated)
                return Fail<AccountDetailDto>(HttpStatusCode.Forbidden, "ReauthenticationRequired", Reauth().Message);
        }
        return role switch
        {
            "Merchant" => await CreateMerchantAsync(command),
            "Shipper" => await CreateShipperAsync(command),
            _ => await IdentitySafeAsync(() => CreateUserAsync(caller, role, command))
        };
    }

    // Merchant/Shipper accounts are created by the existing partner services (account + profile in one transaction).
    private async Task<BaseResponse<AccountDetailDto>> CreateMerchantAsync(CreateAccountCommand command)
    {
        if (command.Merchant == null)
            return Invalid<AccountDetailDto>(new() { ["merchant"] = ["Tạo Merchant cần hồ sơ merchant (cùng payload với POST /merchant)."] });
        command.Merchant.ClientRequestId ??= command.OperationId;
        var result = await LazyServiceProvider.LazyGetRequiredService<IMerchantAppService>().CreateAsync(command.Merchant);
        return await FromPartnerAsync(result, result.Data?.Id);
    }

    private async Task<BaseResponse<AccountDetailDto>> CreateShipperAsync(CreateAccountCommand command)
    {
        if (command.Shipper == null)
            return Invalid<AccountDetailDto>(new() { ["shipper"] = ["Tạo Shipper cần hồ sơ shipper (cùng payload với POST /shipper)."] });
        command.Shipper.ClientRequestId ??= command.OperationId;
        var result = await LazyServiceProvider.LazyGetRequiredService<IShipperManagementAppService>().CreateAsync(command.Shipper);
        return await FromPartnerAsync(result, result.Data?.UserId);
    }

    private async Task<BaseResponse<AccountDetailDto>> FromPartnerAsync<T>(BaseResponse<T> result, Guid? userId)
    {
        if (!result.Succeeded || !userId.HasValue)
            return new BaseResponse<AccountDetailDto>
            {
                Succeeded = false, StatusCode = result.StatusCode, Message = result.Message, Errors = result.Errors, Meta = result.Meta
            };
        var detail = await GetAsync(userId.Value);
        detail.StatusCode = result.StatusCode;
        detail.Message = result.Message;
        detail.Meta = result.Meta;
        return detail;
    }

    private async Task<BaseResponse<AccountDetailDto>> CreateUserAsync(Caller caller, string role, CreateAccountCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (AccountRules.FullNameError(command.FullName) is { } nameError) errors["fullName"] = [nameError];
        var email = AccountRules.NormalizeEmail(command.Email);
        if (!AccountRules.IsValidEmail(command.Email)) errors["email"] = ["Email không đúng định dạng (tối đa 100 ký tự)."];
        var phone = command.Phone?.Trim();
        if (!CustomerPhoneRules.IsValid(phone)) errors["phone"] = ["Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0."];
        var avatar = string.IsNullOrWhiteSpace(command.AvatarUrl) ? null : command.AvatarUrl.Trim();
        if (avatar != null && await AvatarErrorAsync(caller.Id, avatar) is { } avatarError) errors["avatarUrl"] = [avatarError];
        if (errors.Count > 0) return Invalid<AccountDetailDto>(errors);

        string temporaryPassword;
        User user;
        BaseResponse<AccountDetailDto> response;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            if (await Writes.ReplayAsync<AccountDetailDto>(command.OperationId, caller.Id, Module, "Create", "new", command) is { } replay)
            {
                if (replay.Succeeded && replay.Data != null && await users.FindAsync(replay.Data.Id) is { } created)
                {
                    replay.Data.CredentialDelivery = created.CredentialDelivery;
                    replay.Data.CredentialSentAt = Utc(created.CredentialSentAt);
                }
                return replay;
            }
            await identity.LockEmailAsync(email);
            if (await identity.EmailInUseAsync(email))
                return AccountIdentityGuard.EmailConflict<AccountDetailDto>();
            var roleEntity = await roles.FirstOrDefaultAsync(x => x.Name == role);
            if (roleEntity == null)
                return Fail<AccountDetailDto>(HttpStatusCode.Conflict, "RoleMissing", "Không tìm thấy vai trò trong hệ thống.");
            temporaryPassword = TemporaryPassword.Generate();
            var now = DateTime.UtcNow;
            user = new User(GuidGenerator.Create())
            {
                RoleId = roleEntity.Id, Email = email, PasswordHash = PasswordHasher.HashPassword(temporaryPassword),
                AuthProvider = AuthProvider.Local, FullName = command.FullName!.Trim(), Phone = phone, Avatar = avatar,
                IsActive = true, CreatedAt = now, UpdatedAt = now, CredentialDelivery = CredentialDeliveryStatus.Unknown
            };
            await users.InsertAsync(user, autoSave: true);
            if (role == "Customer")
                await customers.InsertAsync(new Customer(user.Id) { CreatedAt = now, UpdatedAt = now }, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "Create", "User", user.Id.ToString(), user.FullName)
            {
                Details = new { role, operationId = command.OperationId }
            });
            response = Created(await DetailAsync(caller, new Target { User = user, Role = role }),
                "Đã tạo tài khoản; kết quả gửi email mật khẩu tạm trả trong credentialDelivery.");
            await Writes.RecordAsync(command.OperationId, caller.Id, Module, "Create", "new", command, response);
            await uow.CompleteAsync();
        }
        // The account is committed before the email: a failed delivery never creates a second account on retry.
        var delivery = CredentialDeliveryStatus.Unknown;
        try { delivery = await emailSender.SendRoleCredentialsAsync(email, user.FullName, role, temporaryPassword); }
        catch (Exception) { logger.LogWarning("Account {UserId} credential email outcome is unknown.", user.Id); }
        try
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var saved = await users.GetAsync(user.Id);
            saved.CredentialDelivery = delivery;
            saved.CredentialSentAt = delivery == CredentialDeliveryStatus.Sent ? DateTime.UtcNow : null;
            await users.UpdateAsync(saved, autoSave: true);
            await uow.CompleteAsync();
            response.Data!.CredentialDelivery = saved.CredentialDelivery;
            response.Data.CredentialSentAt = Utc(saved.CredentialSentAt);
        }
        catch (Exception)
        {
            logger.LogWarning("Account {UserId} created; credential delivery metadata could not be saved.", user.Id);
        }
        return response;
    }

    private async Task<string?> AvatarErrorAsync(Guid ownerId, string url) =>
        (await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), [url], ownerId, 1)).Error == null
            ? null
            : "Ảnh đại diện phải là ảnh do chính bạn upload qua /files/images.";

    /// <summary>A unique violation on identity columns (race past the advisory lock) is a 409, not a 500.</summary>
    private async Task<BaseResponse<T>> IdentitySafeAsync<T>(Func<Task<BaseResponse<T>>> action)
    {
        try { return await action(); }
        catch (Exception error) when (identity.Classify(error) is { } code)
        {
            return code == "EmailAlreadyUsed"
                ? AccountIdentityGuard.EmailConflict<T>()
                : Fail<T>(HttpStatusCode.Conflict, "IdentityAlreadyInUse", "Email hoặc tài khoản Google đã được tài khoản khác sử dụng.",
                    default, new() { ["email"] = ["Email hoặc tài khoản Google đã được tài khoản khác sử dụng."] });
        }
    }

    // ───────────────────────────── single-account writes ─────────────────────────────

    /// <summary>
    /// Operation key → account lock → Admin guard lock (when an Admin is involved) → re-read caller and target →
    /// rules → re-authentication → revision → body. Only successful results are stored for replay.
    /// </summary>
    private Task<BaseResponse<AccountDetailDto>> WriteAsync(Guid id, string action, Guid? operationId, object payload, long? expectedRevision,
        Func<IUnitOfWork, Caller, Target, IReadOnlyCollection<Guid>, Task<BaseResponse<AccountDetailDto>>> body, Func<Target, string>? ruleAction = null) =>
        IdentitySafeAsync(async () =>
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var caller = await CallerAsync();
            if (caller == null) return NotAdmin<AccountDetailDto>();
            if (await Writes.ReplayAsync<AccountDetailDto>(operationId, caller.Id, Module, action, id.ToString(), payload) is { } replay)
                return replay;
            await Writes.LockAsync($"account:{id}");
            var roleName = await AsyncExecuter.FirstOrDefaultAsync(
                from u in await users.GetQueryableAsync()
                join r in await roles.GetQueryableAsync() on u.RoleId equals r.Id
                where u.Id == id
                select r.Name);
            if (roleName == null) return Missing<AccountDetailDto>();
            if (roleName == "Admin")
            {
                await permissions.LockAdminGuardAsync();
                caller = await CallerAsync();
                if (caller == null) return NotAdmin<AccountDetailDto>();
            }
            var target = await TargetAsync(id);
            if (target == null || target.User.IsDeleted && action != "Restore" && !caller.Has(AccountPermissions.ReadDeleted))
                return Missing<AccountDetailDto>();
            var privileged = roleName == "Admin" ? await permissions.PrivilegedAdminIdsAsync() : new List<Guid>();
            var rule = ruleAction?.Invoke(target) ?? RuleAction(action);
            if (Rule(caller, target.Subject, rule, privileged) is { } denial)
                return await DenyAsync(uow, target, action, denial);
            if (target.Role == "Admin" && !caller.Reauthenticated)
                return await DenyAsync(uow, target, action, Reauth());
            if (!expectedRevision.HasValue)
                return Invalid<AccountDetailDto>(new() { ["expectedRevision"] = ["Thiếu expectedRevision (revision từ GET hoặc header If-Match)."] });
            if (expectedRevision.Value != target.User.Revision)
                return Fail(HttpStatusCode.Conflict, "RevisionConflict", "Tài khoản đã được thay đổi ở nơi khác; vui lòng tải lại.",
                    await DetailAsync(caller, target), null, new { code = "RevisionConflict", currentRevision = target.User.Revision });
            var response = await body(uow, caller, target, privileged);
            if (response.Succeeded)
                await Writes.RecordAsync(operationId, caller.Id, Module, action, id.ToString(), payload, response);
            await uow.CompleteAsync();
            return response;
        });

    private static BaseResponse<AccountDetailDto> Unchanged(AccountDetailDto data, string message) =>
        AdministrativeBatchExecutor.Unchanged(new BaseResponse<AccountDetailDto>
        {
            Succeeded = true, StatusCode = HttpStatusCode.OK, Data = data, Message = message
        });

    public Task<BaseResponse<AccountDetailDto>> UpdateAsync(Guid id, UpdateAccountCommand command) =>
        WriteAsync(id, "Update", command.OperationId, command, command.ExpectedRevision, async (uow, caller, target, privileged) =>
        {
            var u = target.User;
            var errors = new Dictionary<string, string[]>();
            string? fullName = null, phone = null, avatar = null;
            if (command.FullName != null)
            {
                if (AccountRules.FullNameError(command.FullName) is { } nameError) errors["fullName"] = [nameError];
                else fullName = command.FullName.Trim();
            }
            if (command.Phone != null)
            {
                phone = string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim();
                if (phone != null && !CustomerPhoneRules.IsValid(phone)) errors["phone"] = ["Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0."];
                if (phone == null && target.Role is "Customer" or "Shipper") errors["phone"] = ["Tài khoản Customer/Shipper cần số điện thoại."];
            }
            if (command.Email != null && AccountRules.NormalizeEmail(command.Email) != AccountRules.NormalizeEmail(u.Email))
                errors["email"] = ["Đổi email cần xác minh địa chỉ mới: dùng POST /admin/accounts/{id}/email-change."];
            if (command.AvatarUrl != null)
            {
                avatar = string.IsNullOrWhiteSpace(command.AvatarUrl) ? null : command.AvatarUrl.Trim();
                if (avatar != null && avatar != u.Avatar && await AvatarErrorAsync(caller.Id, avatar) is { } avatarError)
                    errors["avatarUrl"] = [avatarError];
            }
            if (errors.Count > 0)
                return errors.ContainsKey("email") && errors.Count == 1
                    ? Fail<AccountDetailDto>(HttpStatusCode.BadRequest, "UseEmailChangeFlow", errors["email"][0], default, errors)
                    : Invalid<AccountDetailDto>(errors);
            // Shipper phones identify the courier (same rule as the Shipper page): unique among accounts not deleted.
            // Checked before anything is changed so a refused request leaves the tracked account untouched.
            if (command.Phone != null && phone != u.Phone && target.Role == "Shipper" && phone != null)
            {
                await Writes.LockAsync($"shipper-phone:{phone}");
                if (await users.AnyAsync(x => x.Phone == phone && !x.IsDeleted && x.Id != u.Id))
                    return Fail<AccountDetailDto>(HttpStatusCode.Conflict, "PhoneAlreadyInUse", "Số điện thoại này đã được sử dụng.",
                        default, new() { ["phone"] = ["Số điện thoại này đã được sử dụng."] });
            }
            var changed = new List<string>();
            if (fullName != null && fullName != u.FullName) { u.FullName = fullName; changed.Add("fullName"); }
            if (command.Phone != null && phone != u.Phone) { u.Phone = phone; changed.Add("phone"); }
            if (command.AvatarUrl != null && avatar != u.Avatar) { u.Avatar = avatar; changed.Add("avatarUrl"); }
            if (changed.Count == 0)
                return Unchanged(await DetailAsync(caller, target), "Không có thay đổi.");
            u.UpdatedAt = DateTime.UtcNow;
            await users.UpdateAsync(u, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "Update", "User", u.Id.ToString(), u.FullName)
            {
                ChangedFields = changed, Details = new { role = target.Role, operationId = command.OperationId }
            });
            return Success(await DetailAsync(caller, target), "Đã cập nhật tài khoản.");
        });

    public Task<BaseResponse<AccountDetailDto>> SetStatusAsync(Guid id, SetAccountStatusCommand command)
    {
        if (!command.IsActive.HasValue)
            return Task.FromResult(Invalid<AccountDetailDto>(new() { ["isActive"] = ["Cần isActive đích (true = mở, false = khóa)."] }));
        return SetStatusCoreAsync(id, command);
    }

    private Task<BaseResponse<AccountDetailDto>> SetStatusCoreAsync(Guid id, SetAccountStatusCommand command)
    {
        var locking = command.IsActive == false;
        return WriteAsync(id, locking ? "Lock" : "Unlock", command.OperationId, command, command.ExpectedRevision,
            locking ? (uow, caller, target, _) => LockBodyAsync(caller, target, command) : (uow, caller, target, _) => UnlockBodyAsync(caller, target, command));
    }

    private async Task<BaseResponse<AccountDetailDto>> LockBodyAsync(Caller caller, Target target, SetAccountStatusCommand command)
    {
        if (AccountRules.ReasonError(command.Reason, required: true) is { } reasonError)
            return Invalid<AccountDetailDto>(new() { ["reason"] = [reasonError] });
        var reason = command.Reason!.Trim();
        var u = target.User;
        if (!u.IsActive)
            return Unchanged(await DetailAsync(caller, target), "Tài khoản đã bị khóa trước đó.");
        Shipper? shipper = null;
        if (target.Role == "Shipper" && target.Shipper != null)
        {
            // Same row lock as assignment: a lock and a new task for this courier cannot cross.
            using (dataFilter.Disable<ISoftDelete>())
                shipper = await shippers.GetForUpdateAsync(target.Shipper.Id);
            target.Shipper = shipper;
        }
        if (target.Role == "Merchant")
            await LazyServiceProvider.LazyGetRequiredService<MerchantWriteGuard>().LockMerchantAsync(u.Id);
        var owed = await obligations.ReadAsync(u.Id, target.Role, shipper?.Id);
        if (target.Role == "Shipper" && owed.ActiveTasks > 0)
            return Fail(HttpStatusCode.Conflict, "HasActiveTasks",
                $"Shipper đang có {owed.ActiveTasks} nhiệm vụ lấy/trả chưa hoàn tất; hãy để Shipper hoàn tất hoặc phân công người khác trước khi khóa.",
                await DetailAsync(caller, target), null, new { code = "HasActiveTasks", activeTasks = owed.ActiveTasks });
        if (target.Role is "Customer" or "Merchant" && owed.OpenOrders > 0 && !command.Confirm)
            return Fail(HttpStatusCode.Conflict, "ConfirmationRequired",
                $"Tài khoản còn {owed.OpenOrders} đơn đang xử lý; nhân viên vẫn xử lý được các đơn này. Gửi confirm = true để vẫn khóa.",
                await DetailAsync(caller, target), null, new { code = "ConfirmationRequired", openOrders = owed.OpenOrders });

        await lifecycle.LockAsync(u, caller.Id, reason);
        await users.UpdateAsync(u, autoSave: true);
        if (shipper != null)
        {
            shipper.LockedAt = u.StatusChangedAt;
            shipper.LockReason = reason;
            shipper.UpdatedAt = DateTime.UtcNow;
            await shippers.UpdateAsync(shipper, autoSave: true);
            await ShipperAuditAsync(caller, shipper, ShipperAuditAction.Locked, reason);
        }
        await Audit.LogAsync(new AuditEntry(Module, "Lock", "User", u.Id.ToString(), u.FullName)
        {
            Reason = reason, ChangedFields = ["isActive"],
            Details = new { role = target.Role, operationId = command.OperationId, openOrders = owed.OpenOrders, confirm = command.Confirm }
        });
        return Success(await DetailAsync(caller, target), "Đã khóa tài khoản; mọi phiên đăng nhập đã bị thu hồi.");
    }

    private async Task<BaseResponse<AccountDetailDto>> UnlockBodyAsync(Caller caller, Target target, SetAccountStatusCommand command)
    {
        if (AccountRules.ReasonError(command.Reason, required: false) is { } reasonError)
            return Invalid<AccountDetailDto>(new() { ["reason"] = [reasonError] });
        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        var u = target.User;
        if (u.IsActive)
            return Unchanged(await DetailAsync(caller, target), "Tài khoản đang hoạt động.");
        // Sign-in only: business status, work status and old assignments stay as they are; old sessions do not come back.
        lifecycle.Unlock(u, caller.Id, reason);
        await users.UpdateAsync(u, autoSave: true);
        if (target.Shipper is { IsDeleted: false } shipper)
        {
            shipper.LockedAt = null;
            shipper.LockReason = null;
            shipper.UpdatedAt = DateTime.UtcNow;
            await shippers.UpdateAsync(shipper, autoSave: true);
            await ShipperAuditAsync(caller, shipper, ShipperAuditAction.Unlocked, reason);
        }
        await Audit.LogAsync(new AuditEntry(Module, "Unlock", "User", u.Id.ToString(), u.FullName)
        {
            Reason = reason, ChangedFields = ["isActive"], Details = new { role = target.Role, operationId = command.OperationId }
        });
        return Success(await DetailAsync(caller, target), "Đã mở tài khoản; người dùng cần đăng nhập lại.");
    }

    private Task ShipperAuditAsync(Caller caller, Shipper shipper, ShipperAuditAction action, string? reason) =>
        shipperAudits.InsertAsync(new ShipperAudit(GuidGenerator.Create())
        {
            ShipperId = shipper.Id, MerchantId = shipper.MerchantId, Action = action, Reason = reason,
            ActorId = caller.Id, ActorRole = "Admin", ActorName = caller.Name, CreatedAt = DateTime.UtcNow
        }, autoSave: true);

    public Task<BaseResponse<AccountDetailDto>> SoftDeleteAsync(Guid id, SoftDeleteAccountCommand command) =>
        WriteAsync(id, "SoftDelete", command.OperationId, command, command.ExpectedRevision, async (uow, caller, target, privileged) =>
        {
            if (AccountRules.ReasonError(command.Reason, required: true) is { } reasonError)
                return Invalid<AccountDetailDto>(new() { ["reason"] = [reasonError] });
            var reason = command.Reason!.Trim();
            var u = target.User;
            if (u.IsDeleted)
                return Unchanged(await DetailAsync(caller, target), "Tài khoản đã bị xóa trước đó; thời điểm xóa giữ nguyên.");
            Shipper? shipper = null;
            if (target.Shipper != null)
            {
                using (dataFilter.Disable<ISoftDelete>())
                    shipper = await shippers.GetForUpdateAsync(target.Shipper.Id);
                target.Shipper = shipper;
            }
            if (target.Role == "Merchant")
                await LazyServiceProvider.LazyGetRequiredService<MerchantWriteGuard>().LockMerchantAsync(u.Id);
            var owed = await obligations.ReadAsync(u.Id, target.Role, shipper?.Id);
            if (owed.DeleteBlockers.Count > 0)
                return Fail(HttpStatusCode.Conflict, "AccountHasOpenObligations",
                    "Tài khoản còn nghĩa vụ chưa xử lý (" + string.Join(", ", owed.DeleteBlockers) + "); có thể khóa trước rồi bàn giao/giải quyết, sau đó mới xóa.",
                    await DetailAsync(caller, target), null, new { code = "AccountHasOpenObligations", blockers = owed.DeleteBlockers });

            var now = DateTime.UtcNow;
            if (shipper is { IsDeleted: false })
            {
                shipper.IsDeleted = true;
                shipper.DeletedAt = now;
                shipper.DeletedBy = caller.Id;
                shipper.IsActive = false;
                shipper.UpdatedAt = now;
                await shippers.UpdateAsync(shipper, autoSave: true);
                await ShipperAuditAsync(caller, shipper, ShipperAuditAction.Deleted, reason);
            }
            if (target.Merchant is { Status: MerchantStatus.Active } merchant)
            {
                // The profile and its history stay; a deleted account's store no longer trades or receives orders.
                merchant.Status = MerchantStatus.Inactive;
                merchant.RejectionReason = reason;
                MerchantWriteGuard.Touch(merchant);
                await merchants.UpdateAsync(merchant, autoSave: true);
            }
            List<string> revoked = [];
            if (target.Role == "Admin")
                revoked = (await permissions.SetAsync(u.Id, [], caller.Id)).Removed;
            await lifecycle.SoftDeleteAsync(u, caller.Id, reason);
            await users.UpdateAsync(u, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "SoftDelete", "User", u.Id.ToString(), u.FullName)
            {
                Reason = reason, ChangedFields = ["isDeleted", "isActive"],
                Details = new { role = target.Role, operationId = command.OperationId, removed = revoked }
            });
            return Success(await DetailAsync(caller, target), "Đã xóa mềm tài khoản; lịch sử và dữ liệu liên quan được giữ nguyên.");
        });

    public Task<BaseResponse<AccountDetailDto>> RestoreAsync(Guid id, RestoreAccountCommand command) =>
        WriteAsync(id, "Restore", command.OperationId, command, command.ExpectedRevision, async (uow, caller, target, privileged) =>
        {
            if (AccountRules.ReasonError(command.Reason, required: false) is { } reasonError)
                return Invalid<AccountDetailDto>(new() { ["reason"] = [reasonError] });
            var u = target.User;
            var email = AccountRules.NormalizeEmail(u.Email);
            await identity.LockEmailAsync(email);
            var providerTaken = u.ProviderId != null && await users.AnyAsync(x =>
                x.AuthProvider == u.AuthProvider && x.ProviderId == u.ProviderId && !x.IsDeleted && x.Id != u.Id);
            if (providerTaken || await identity.EmailInUseAsync(email, u.Id))
                return Fail(HttpStatusCode.Conflict, "IdentityAlreadyInUse",
                    "Email hoặc tài khoản Google của tài khoản này đã được tài khoản mới sử dụng; không tự gộp hoặc đổi email.",
                    await DetailAsync(caller, target), new() { ["email"] = ["Email đã được tài khoản khác sử dụng."] });
            var now = DateTime.UtcNow;
            u.IsDeleted = false;
            u.DeletedAt = null;
            u.DeletedBy = null;
            u.DeleteReason = null;
            // Restored accounts start locked: no session, no automatic re-activation of an Admin.
            u.IsActive = false;
            u.StatusChangedAt = now;
            u.StatusChangedBy = caller.Id;
            u.StatusReason = string.IsNullOrWhiteSpace(command.Reason) ? "Khôi phục tài khoản; chờ xét mở lại." : command.Reason.Trim();
            u.UpdatedAt = now;
            await users.UpdateAsync(u, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "Restore", "User", u.Id.ToString(), u.FullName)
            {
                Reason = command.Reason?.Trim(), ChangedFields = ["isDeleted"], Details = new { role = target.Role, operationId = command.OperationId }
            });
            return Success(await DetailAsync(caller, target), "Đã khôi phục tài khoản ở trạng thái khóa; mở lại khi đủ điều kiện.");
        });

    public Task<BaseResponse<AccountDetailDto>> UpdatePermissionsAsync(Guid id, UpdateAccountPermissionsCommand command) =>
        WriteAsync(id, "Permissions", command.OperationId, command, command.ExpectedRevision, async (uow, caller, target, privileged) =>
        {
            var requested = (command.Permissions ?? []).Select(x => x?.Trim() ?? string.Empty).Distinct().ToList();
            if (requested.Any(x => !AccountPermissions.IsGrantable(x)))
                return Invalid<AccountDetailDto>(new() { ["permissions"] = ["Chỉ nhận: " + string.Join(", ", AccountPermissions.Grantable) + "."] });
            if (AccountRules.ReasonError(command.Reason, required: true) is { } reasonError)
                return Invalid<AccountDetailDto>(new() { ["reason"] = [reasonError] });
            var u = target.User;
            if (!requested.Contains(AccountPermissions.ManageAdmins) && privileged.Contains(u.Id) && privileged.All(x => x == u.Id))
                return await DenyAsync(null, target, "Permissions", new(HttpStatusCode.Conflict, "LastPrivilegedAdmin",
                    "Không thể gỡ quyền Accounts.ManageAdmins của Admin quản trị cuối cùng."));
            var (added, removed) = await permissions.SetAsync(u.Id, requested, caller.Id);
            if (added.Count == 0 && removed.Count == 0)
                return Unchanged(await DetailAsync(caller, target), "Quyền không thay đổi.");
            await Audit.LogAsync(new AuditEntry(Module, "Permissions", "User", u.Id.ToString(), u.FullName)
            {
                Reason = command.Reason!.Trim(), ChangedFields = ["permissions"],
                Details = new { role = target.Role, operationId = command.OperationId, added, removed, permissions = requested }
            });
            return Success(await DetailAsync(caller, target), "Đã cập nhật quyền quản lý tài khoản.");
        });

    // ───────────────────────────── email change ─────────────────────────────

    public async Task<BaseResponse<AccountEmailChangeDto>> RequestEmailChangeAsync(Guid id, RequestAccountEmailChangeCommand command)
    {
        var newEmail = AccountRules.NormalizeEmail(command.NewEmail);
        if (!AccountRules.IsValidEmail(command.NewEmail))
            return Invalid<AccountEmailChangeDto>(new() { ["newEmail"] = ["Email không đúng định dạng (tối đa 100 ký tự)."] });
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var caller = await CallerAsync();
            if (caller == null) return NotAdmin<AccountEmailChangeDto>();
            await Writes.LockAsync($"account:{id}");
            var target = await TargetAsync(id);
            if (target == null || target.User.IsDeleted && !caller.Has(AccountPermissions.ReadDeleted))
                return Missing<AccountEmailChangeDto>();
            var privileged = target.Role == "Admin" ? await permissions.PrivilegedAdminIdsAsync() : new List<Guid>();
            var denial = Rule(caller, target.Subject, "ChangeEmail", privileged) ?? (target.Role == "Admin" && !caller.Reauthenticated ? Reauth() : null);
            if (denial != null)
            {
                await DenyAsync(uow, target, "EmailChangeRequested", denial);
                return Fail<AccountEmailChangeDto>(denial.Status, denial.Code, denial.Message);
            }
            if (command.ExpectedRevision.HasValue && command.ExpectedRevision != target.User.Revision)
                return Fail<AccountEmailChangeDto>(HttpStatusCode.Conflict, "RevisionConflict", "Tài khoản đã được thay đổi ở nơi khác; vui lòng tải lại.");
            if (newEmail == AccountRules.NormalizeEmail(target.User.Email))
                return Invalid<AccountEmailChangeDto>(new() { ["newEmail"] = ["Email mới trùng email hiện tại."] });
            if (await identity.EmailInUseAsync(newEmail, id))
                return Fail<AccountEmailChangeDto>(HttpStatusCode.Conflict, AccountIdentityGuard.EmailInUseCode, AccountIdentityGuard.EmailInUseMessage,
                    default, new() { ["newEmail"] = [AccountIdentityGuard.EmailInUseMessage] });
            var recent = DateTime.UtcNow.AddSeconds(-60);
            if (await otps.AnyAsync(x => x.UserId == id && x.Purpose == OtpPurpose.ChangeEmail && !x.IsUsed && x.CreatedAt > recent))
                return Fail<AccountEmailChangeDto>(HttpStatusCode.TooManyRequests, "OtpThrottled", "Vui lòng chờ 60 giây trước khi gửi lại mã.");
            await uow.CompleteAsync();
        }
        // Mail is sent outside any transaction; the challenge is stored only once the address accepted the message.
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        if (!await emailSender.SendOtpAsync(newEmail, code, OtpPurpose.ChangeEmail))
            return Fail<AccountEmailChangeDto>(HttpStatusCode.ServiceUnavailable, "EmailDeliveryFailed", "Không gửi được mã xác minh tới email mới; vui lòng thử lại sau.");
        var expires = DateTime.UtcNow.AddMinutes(10);
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            var previous = await otps.GetListAsync(x => x.UserId == id && x.Purpose == OtpPurpose.ChangeEmail && !x.IsUsed);
            foreach (var otp in previous) { otp.IsUsed = true; otp.UsedAt = DateTime.UtcNow; }
            if (previous.Count > 0) await otps.UpdateManyAsync(previous, autoSave: true);
            await otps.InsertAsync(new OtpCode(GuidGenerator.Create())
            {
                UserId = id, Email = newEmail, CodeHash = PasswordHasher.HashPassword(code), Purpose = OtpPurpose.ChangeEmail,
                ExpiryDate = expires, MaxAttempts = 5, CreatedAt = DateTime.UtcNow
            }, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "EmailChangeRequested", "User", id.ToString(), null));
            await uow.CompleteAsync();
        }
        return Success(new AccountEmailChangeDto { NewEmail = newEmail, ExpiresAt = Utc(expires) },
            "Đã gửi mã xác minh tới email mới; người dùng cung cấp mã để hoàn tất đổi email.");
    }

    public Task<BaseResponse<AccountDetailDto>> ConfirmEmailChangeAsync(Guid id, ConfirmAccountEmailChangeCommand command) =>
        WriteAsync(id, "EmailChange", command.OperationId, command, command.ExpectedRevision, async (uow, caller, target, privileged) =>
        {
            var newEmail = AccountRules.NormalizeEmail(command.NewEmail);
            if (!AccountRules.IsValidEmail(command.NewEmail) || string.IsNullOrWhiteSpace(command.Code) || command.Code.Trim().Length != 6)
                return Invalid<AccountDetailDto>(new() { ["code"] = ["Mã xác minh không hợp lệ hoặc đã hết hạn."] });
            await identity.LockEmailAsync(newEmail);
            var otp = await otps.GetLatestActiveForUpdateAsync(newEmail, OtpPurpose.ChangeEmail);
            if (otp == null || otp.UserId != target.User.Id)
                return Invalid<AccountDetailDto>(new() { ["code"] = ["Mã xác minh không hợp lệ hoặc đã hết hạn."] });
            if (otp.AttemptCount >= otp.MaxAttempts)
                return Fail<AccountDetailDto>(HttpStatusCode.TooManyRequests, "TooManyAttempts", "Đã nhập sai mã quá số lần cho phép; hãy gửi mã mới.");
            if (!PasswordHasher.Verify(command.Code.Trim(), otp.CodeHash))
            {
                otp.AttemptCount++;
                await otps.UpdateAsync(otp, autoSave: true);
                return Invalid<AccountDetailDto>(new() { ["code"] = [$"Mã xác minh không đúng. Còn {otp.MaxAttempts - otp.AttemptCount} lần thử."] });
            }
            if (await identity.EmailInUseAsync(newEmail, target.User.Id))
                return AccountIdentityGuard.EmailConflict<AccountDetailDto>();
            var u = target.User;
            u.Email = newEmail;
            u.UpdatedAt = DateTime.UtcNow;
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await otps.UpdateAsync(otp, autoSave: true);
            // The Google subject is not moved to the new address; Google sign-in keeps using the linked Google account.
            await users.UpdateAsync(u, autoSave: true);
            await Audit.LogAsync(new AuditEntry(Module, "EmailChange", "User", u.Id.ToString(), u.FullName)
            {
                ChangedFields = ["email"], Details = new { role = target.Role, operationId = command.OperationId }
            });
            return Success(await DetailAsync(caller, target), "Đã đổi email đăng nhập.");
        });

    // ───────────────────────────── bulk ─────────────────────────────

    public async Task<BaseResponse<AccountBulkPreviewDto>> PreviewBulkAsync(AccountBulkPreviewCommand command)
    {
        var caller = await CallerAsync();
        if (caller == null) return NotAdmin<AccountBulkPreviewDto>();
        var action = BulkActions.FirstOrDefault(x => string.Equals(x, command.Action?.Trim(), StringComparison.OrdinalIgnoreCase));
        var ids = command.Ids ?? [];
        if (action == null || ids.Count < 1 || ids.Count > MaxBulk || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            return Invalid<AccountBulkPreviewDto>(new() { ["ids"] = [$"Cần action = Lock|Unlock|SoftDelete và 1–{MaxBulk} ID khác nhau."] });
        var result = new AccountBulkPreviewDto { Action = action };
        var privileged = await permissions.PrivilegedAdminIdsAsync();
        foreach (var id in ids)
        {
            var target = await TargetAsync(id);
            if (target == null || target.User.IsDeleted && !caller.Has(AccountPermissions.ReadDeleted))
            {
                result.Items.Add(new AccountBulkPreviewItemDto { Id = id, ReasonCodes = ["NotFound"] });
                continue;
            }
            var u = target.User;
            var item = new AccountBulkPreviewItemDto
            {
                Id = id, Found = true, FullName = u.FullName, AvatarUrl = string.IsNullOrWhiteSpace(u.Avatar) ? null : u.Avatar,
                Role = target.Role, IsActive = u.IsActive, IsDeleted = u.IsDeleted, Revision = u.Revision,
                AlreadyInTargetState = action switch { "Lock" => !u.IsActive, "Unlock" => u.IsActive && !u.IsDeleted, _ => u.IsDeleted },
                RequiresReauthentication = target.Role == "Admin"
            };
            if (Rule(caller, target.Subject, RuleAction(action), privileged) is { } denial) item.ReasonCodes.Add(denial.Code);
            if (target.Role == "Admin" && !caller.Reauthenticated) item.ReasonCodes.Add("ReauthenticationRequired");
            if (!u.IsDeleted && !item.AlreadyInTargetState && action != "Unlock")
            {
                var owed = await obligations.ReadAsync(u.Id, target.Role, target.Shipper?.Id);
                item.OpenOrders = owed.OpenOrders;
                item.ActiveTasks = owed.ActiveTasks;
                item.DeleteBlockers = owed.DeleteBlockers;
                if (action == "Lock" && target.Role == "Shipper" && owed.ActiveTasks > 0) item.ReasonCodes.Add("HasActiveTasks");
                if (action == "Lock" && target.Role is "Customer" or "Merchant" && owed.OpenOrders > 0) item.RequiresConfirmation = true;
                if (action == "SoftDelete" && owed.DeleteBlockers.Count > 0) item.ReasonCodes.Add("AccountHasOpenObligations");
            }
            item.CanApply = item.ReasonCodes.Count == 0;
            result.Items.Add(item);
        }
        result.CanApply = result.Items.Count(x => x.CanApply);
        result.Blocked = result.Items.Count - result.CanApply;
        return Success(result, "Xem trước không giữ chỗ; khi thực thi mọi điều kiện được kiểm tra lại cho từng tài khoản.");
    }

    public async Task<BaseResponse<AdministrativeBatchResultDto>> BulkAsync(AccountBulkCommand command)
    {
        var action = BulkActions.FirstOrDefault(x => string.Equals(x, command.Action?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (action == null)
            return Invalid<AdministrativeBatchResultDto>(new() { ["action"] = ["action chỉ nhận Lock, Unlock hoặc SoftDelete (không đổi role hàng loạt)."] });
        var error = AccountRules.ReasonError(command.Reason, required: action != "Unlock");
        if (error != null)
            return Invalid<AdministrativeBatchResultDto>(new() { ["reason"] = [error] });
        // Confirm is part of the operation identity: the same key with a different confirm is a different request.
        var batchAction = action == "Lock" && command.Confirm ? "LockConfirmed" : action;
        var batch = new AdministrativeBatchCommand
        {
            OperationId = command.OperationId, Action = batchAction, Reason = command.Reason?.Trim(),
            Items = (command.Items ?? []).Select(x => new AdministrativeBatchItem { Id = x.Id, ExpectedVersion = x.ExpectedRevision.ToString() }).ToList()
        };
        var executor = LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>();
        return await executor.RunAsync(Module, batch, MaxBulk, async (item, childKey) =>
        {
            if (!long.TryParse(item.ExpectedVersion, out var revision))
                return AdministrativeBatchExecutor.Invalid(item.Id, "ExpectedRevisionRequired", "expectedRevision không hợp lệ.");
            var response = action switch
            {
                "SoftDelete" => await SoftDeleteAsync(item.Id, new SoftDeleteAccountCommand { OperationId = childKey, Reason = batch.Reason, ExpectedRevision = revision }),
                _ => await SetStatusCoreAsync(item.Id, new SetAccountStatusCommand
                {
                    OperationId = childKey, IsActive = action == "Unlock", Reason = batch.Reason, ExpectedRevision = revision, Confirm = batchAction == "LockConfirmed"
                })
            };
            var result = AdministrativeBatchExecutor.Item(item.Id, response);
            result.Version = response.Data?.Revision.ToString() ?? result.Version;
            result.IsActive = response.Data?.IsActive ?? result.IsActive;
            return result;
        }, ["Lock", "LockConfirmed", "Unlock", "SoftDelete"]);
    }
}
