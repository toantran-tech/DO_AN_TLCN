using System;
using System.Collections.Generic;

namespace WashGo.Domain.Shared.Constants;

/// <summary>
/// Quyền quản lý tài khoản. Mọi Admin đang hoạt động mặc định có các quyền thao tác tài khoản thông thường
/// (Customer/Merchant/Shipper); các quyền cấp riêng (bảng user_permissions) không mặc định cho mọi Admin.
/// </summary>
public static class AccountPermissions
{
    public const string Read = "Accounts.Read";
    public const string Create = "Accounts.Create";
    public const string Update = "Accounts.Update";
    public const string SetStatus = "Accounts.SetStatus";
    public const string SoftDelete = "Accounts.SoftDelete";
    /// <summary>Kết hợp với quyền hành động tương ứng để tạo/sửa/khóa/xóa/cấp quyền tài khoản Admin.</summary>
    public const string ManageAdmins = "Accounts.ManageAdmins";
    public const string ReadDeleted = "Accounts.ReadDeleted";
    public const string Restore = "Accounts.Restore";

    /// <summary>Quyền mặc định của mọi Admin đang hoạt động (không cần dòng cấp quyền).</summary>
    public static readonly IReadOnlyList<string> AdminDefaults = [Read, Create, Update, SetStatus, SoftDelete];

    /// <summary>Quyền cấp riêng cho từng Admin.</summary>
    public static readonly IReadOnlyList<string> Grantable = [ManageAdmins, ReadDeleted, Restore];

    public static bool IsGrantable(string? permission) =>
        permission != null && ((IList<string>)Grantable).Contains(permission);
}
