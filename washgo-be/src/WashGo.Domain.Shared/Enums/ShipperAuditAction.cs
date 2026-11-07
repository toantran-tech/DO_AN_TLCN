namespace WashGo.Domain.Shared.Enums
{
    /// <summary>Thao tác quản trị tài khoản/hồ sơ Shipper được ghi vết.</summary>
    public enum ShipperAuditAction
    {
        Locked = 1,
        Unlocked = 2,
        Deleted = 3,
        IdentityUpdated = 4
    }
}
