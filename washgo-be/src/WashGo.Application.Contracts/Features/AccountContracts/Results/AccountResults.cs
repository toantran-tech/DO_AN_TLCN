using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AccountContracts.Results;

/// <summary>Một dòng danh sách tài khoản. Không bao giờ chứa hash/token/OTP/provider subject/giấy tờ/ngân hàng.</summary>
public class AccountListItemDto
{
    public Guid Id { get; set; }
    /// <summary>Gửi lại làm expectedRevision khi sửa/khóa/xóa.</summary>
    public long Revision { get; set; }
    /// <summary>null khi chưa có ảnh (FE hiện avatar mặc định).</summary>
    public string? AvatarUrl { get; set; }
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
    /// <summary>Password | Google | PasswordAndGoogle.</summary>
    public string SignInMethod { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public bool GoogleLinked { get; set; }
    public AccountPartnerDto? Partner { get; set; }
    public AccountCapabilitiesDto Capabilities { get; set; } = new();
}

public class AccountPartnerDto
{
    public Guid? MerchantId { get; set; }
    public string? BusinessName { get; set; }
    public MerchantStatus? MerchantStatus { get; set; }
    public Guid? ShipperId { get; set; }
    /// <summary>Merchant quản lý Shipper.</summary>
    public Guid? ShipperMerchantId { get; set; }
    public string? ShipperMerchantName { get; set; }
    public bool? ShipperDeleted { get; set; }
    public CredentialDeliveryStatus? CredentialDelivery { get; set; }
    public DateTime? CredentialSentAt { get; set; }
}

/// <summary>
/// Thao tác người gọi được phép làm, BE tính theo quyền và trạng thái hiện tại. Trên danh sách không tính nghĩa vụ nghiệp vụ
/// (đơn/tiền còn mở); chi tiết và bulk preview tính đầy đủ. Mọi thao tác vẫn được kiểm tra lại khi thực thi.
/// </summary>
public class AccountCapabilitiesDto
{
    public bool CanEdit { get; set; }
    public bool CanChangeEmail { get; set; }
    public bool CanLock { get; set; }
    public bool CanUnlock { get; set; }
    public bool CanDelete { get; set; }
    public bool CanRestore { get; set; }
    public bool CanManagePermissions { get; set; }
    /// <summary>Thao tác trên tài khoản Admin cần nhập lại mật khẩu gần đây (POST /auth/reauthenticate).</summary>
    public bool RequiresReauthentication { get; set; }
    public List<AccountBlockDto> Blocked { get; set; } = [];
}

public class AccountBlockDto
{
    /// <summary>Edit | ChangeEmail | Lock | Unlock | Delete | Restore | ManagePermissions.</summary>
    public string Action { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AccountActorDto
{
    public Guid Id { get; set; }
    public string? FullName { get; set; }
}

public class AccountDetailDto : AccountListItemDto
{
    /// <summary>Lần khóa/mở gần nhất; lịch sử đầy đủ ở /activities.</summary>
    public DateTime? StatusChangedAt { get; set; }
    public AccountActorDto? StatusChangedBy { get; set; }
    public string? StatusReason { get; set; }
    public AccountActorDto? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }
    /// <summary>Kết quả gửi mật khẩu tạm (Customer/Admin tạo từ trang này); Merchant/Shipper xem ở partner.</summary>
    public CredentialDeliveryStatus? CredentialDelivery { get; set; }
    public DateTime? CredentialSentAt { get; set; }
    /// <summary>Quyền cấp riêng (chỉ với tài khoản Admin).</summary>
    public List<string>? Permissions { get; set; }
    public AccountObligationsDto Obligations { get; set; } = new();
}

/// <summary>Nghĩa vụ nghiệp vụ còn mở ảnh hưởng khóa/xóa. Xóa mềm bị chặn khi có bất kỳ mục chặn nào.</summary>
public class AccountObligationsDto
{
    public int OpenOrders { get; set; }
    public int PendingPayments { get; set; }
    public int OpenRefunds { get; set; }
    public int OpenIncidents { get; set; }
    public int UnsettledSettlements { get; set; }
    public int ActiveShippers { get; set; }
    public int LockerAssignments { get; set; }
    public int ActiveTasks { get; set; }
    public long LoyaltyPoints { get; set; }
    public long HeldLoyaltyPoints { get; set; }
    /// <summary>Mã các nghĩa vụ đang chặn xóa mềm (ví dụ OpenOrders, OpenRefunds).</summary>
    public List<string> DeleteBlockers { get; set; } = [];
    /// <summary>Tối đa 20 đơn/nhiệm vụ cần bàn giao hoặc xử lý trước.</summary>
    public List<AccountObligationItemDto> Items { get; set; } = [];
}

public class AccountObligationItemDto
{
    /// <summary>Order | Task.</summary>
    public string Type { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public WashOrderStatus Status { get; set; }
}

public class AccountPageDto
{
    public List<AccountListItemDto> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int Take { get; set; }
    public bool HasMore { get; set; }
}

public class AccountSummaryDto
{
    /// <summary>Tài khoản chưa xóa.</summary>
    public int Total { get; set; }
    public int Active { get; set; }
    public int Locked { get; set; }
    /// <summary>null khi người gọi không có quyền Accounts.ReadDeleted.</summary>
    public int? Deleted { get; set; }
    public Dictionary<string, AccountRoleCountDto> ByRole { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}

public class AccountRoleCountDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Locked { get; set; }
    public int? Deleted { get; set; }
}

public class AccountEmailChangeDto
{
    public string NewEmail { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class AccountBulkPreviewDto
{
    public string Action { get; set; } = string.Empty;
    public int CanApply { get; set; }
    public int Blocked { get; set; }
    public List<AccountBulkPreviewItemDto> Items { get; set; } = [];
}

public class AccountBulkPreviewItemDto
{
    public Guid Id { get; set; }
    public bool Found { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDeleted { get; set; }
    public long? Revision { get; set; }
    /// <summary>Tài khoản đã ở trạng thái đích (thực thi sẽ trả Unchanged).</summary>
    public bool AlreadyInTargetState { get; set; }
    public bool CanApply { get; set; }
    public bool RequiresConfirmation { get; set; }
    public bool RequiresReauthentication { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public int OpenOrders { get; set; }
    public int ActiveTasks { get; set; }
    public List<string> DeleteBlockers { get; set; } = [];
}
