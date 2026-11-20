using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Identity;

/// <summary>Quyền cấp riêng cho một Admin (xem <see cref="AccountPermissions.Grantable"/>); mỗi (UserId, Permission) một dòng.</summary>
[SevagoTable(WashGoDbProperties.UserPermissions, Schema = WashGoDbProperties.Schema)]
public class UserPermission : Entity<Guid>
{
    public UserPermission() { }
    public UserPermission(Guid id) : base(id) { }

    public Guid UserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    /// <summary>null = cấp bởi di trú/seed.</summary>
    public Guid? GrantedBy { get; set; }
}
