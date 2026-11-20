using System;
using System.Collections.Generic;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;

namespace WashGo.Application.Contracts.Features.AccountContracts.Commands;

/// <summary>
/// Tạo tài khoản. Customer/Admin dùng các trường chung; Merchant/Shipper gửi hồ sơ đối tác trong <see cref="Merchant"/> /
/// <see cref="Shipper"/> (cùng payload với POST /merchant, POST /shipper) và đi qua đúng dịch vụ tạo đối tác hiện hành.
/// </summary>
public class CreateAccountCommand
{
    /// <summary>Khóa chống tạo trùng; gửi lại cùng khóa + cùng payload trả đúng kết quả cũ, không tạo/gửi thư thêm.</summary>
    public Guid? OperationId { get; set; }
    /// <summary>Customer | Admin | Merchant | Shipper. Không nhận RoleId.</summary>
    public string Role { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Ảnh do chính Admin upload qua /files/images (tùy chọn).</summary>
    public string? AvatarUrl { get; set; }
    public CreateMerchantCommand? Merchant { get; set; }
    public CreateShipperCommand? Shipper { get; set; }
}

/// <summary>Sửa thông tin tài khoản. Trường null = giữ nguyên; chuỗi rỗng ở phone/avatarUrl = xóa.</summary>
public class UpdateAccountCommand
{
    public Guid? OperationId { get; set; }
    /// <summary>Bắt buộc (hoặc header If-Match); lấy từ revision của GET.</summary>
    public long? ExpectedRevision { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>Không đổi email ở đây; email khác hiện tại trả 400 UseEmailChangeFlow.</summary>
    public string? Email { get; set; }
}

/// <summary>Khóa/mở với trạng thái đích rõ ràng (không toggle).</summary>
public class SetAccountStatusCommand
{
    public Guid? OperationId { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>Bắt buộc khi khóa; tùy chọn khi mở (tối đa 500 ký tự).</summary>
    public string? Reason { get; set; }
    public long? ExpectedRevision { get; set; }
    /// <summary>Xác nhận khóa khi Customer/Merchant còn đơn đang xử lý (lần đầu trả 409 ConfirmationRequired kèm số đơn).</summary>
    public bool Confirm { get; set; }
}

public class SoftDeleteAccountCommand
{
    public Guid? OperationId { get; set; }
    public string? Reason { get; set; }
    public long? ExpectedRevision { get; set; }
}

public class RestoreAccountCommand
{
    public Guid? OperationId { get; set; }
    public string? Reason { get; set; }
    public long? ExpectedRevision { get; set; }
}

/// <summary>Đặt toàn bộ quyền cấp riêng của một Admin (Accounts.ManageAdmins, Accounts.ReadDeleted, Accounts.Restore).</summary>
public class UpdateAccountPermissionsCommand
{
    public Guid? OperationId { get; set; }
    public List<string> Permissions { get; set; } = [];
    public string? Reason { get; set; }
    public long? ExpectedRevision { get; set; }
}

/// <summary>Bước 1 đổi email: gửi OTP tới địa chỉ mới để chứng minh quyền sở hữu.</summary>
public class RequestAccountEmailChangeCommand
{
    public string NewEmail { get; set; } = string.Empty;
    public long? ExpectedRevision { get; set; }
}

/// <summary>Bước 2 đổi email: mã OTP chủ địa chỉ mới nhận được.</summary>
public class ConfirmAccountEmailChangeCommand
{
    public Guid? OperationId { get; set; }
    public string NewEmail { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long? ExpectedRevision { get; set; }
}

public class AccountBulkPreviewCommand
{
    /// <summary>Lock | Unlock | SoftDelete.</summary>
    public string Action { get; set; } = string.Empty;
    public List<Guid> Ids { get; set; } = [];
}

public class AccountBulkCommand
{
    public Guid OperationId { get; set; }
    /// <summary>Lock | Unlock | SoftDelete. Không đổi role hàng loạt.</summary>
    public string Action { get; set; } = string.Empty;
    public List<AccountBulkItem> Items { get; set; } = [];
    public string? Reason { get; set; }
    /// <summary>Xác nhận khóa tài khoản còn đơn đang xử lý (áp dụng từng dòng như thao tác đơn lẻ).</summary>
    public bool Confirm { get; set; }
}

public class AccountBulkItem
{
    public Guid Id { get; set; }
    public long ExpectedRevision { get; set; }
}
