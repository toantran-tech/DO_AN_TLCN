namespace WashGo.Application.Contracts.Features.AccountContracts.Queries;

public class AccountListQuery
{
    public int? Page { get; set; }
    /// <summary>1–100, mặc định 20.</summary>
    public int? Take { get; set; }
    /// <summary>Tìm trong họ tên, email, số điện thoại, tên cửa hàng.</summary>
    public string? Keyword { get; set; }
    /// <summary>Customer | Merchant | Shipper | Admin.</summary>
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>Unix giây (UTC), bao gồm hai đầu.</summary>
    public long? CreatedFrom { get; set; }
    public long? CreatedTo { get; set; }
    /// <summary>Exclude (mặc định) | Include | Only. Include/Only cần quyền Accounts.ReadDeleted.</summary>
    public string? Deleted { get; set; }
    /// <summary>createdAt (mặc định) | lastLoginAt | fullName | deletedAt (chỉ khi xem tài khoản đã xóa).</summary>
    public string? SortBy { get; set; }
    /// <summary>asc | desc (mặc định desc, riêng fullName mặc định asc).</summary>
    public string? SortDirection { get; set; }
}

public class AccountActivityQuery
{
    public int? Page { get; set; }
    public int? Take { get; set; }
}
