using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Commands
{
    public class CreateShipperCommand
    {
        /// <summary>Chỉ Admin cần nhập; Merchant luôn tạo Shipper cho tiệm của mình.</summary>
        public Guid? MerchantId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public VehicleType VehicleType { get; set; } = VehicleType.Motorbike;
        public string? VehiclePlate { get; set; }
        public string? IdentityNumber { get; set; }
        /// <summary>URL ảnh CMND/CCCD đã upload qua POST /files/images.</summary>
        public List<string> IdentityImageUrls { get; set; } = [];
    }

    /// <summary>Khóa (lý do bắt buộc) / mở khóa (lý do tùy chọn) tài khoản Shipper; tối đa 500 ký tự.</summary>
    public class ShipperAccountActionCommand
    {
        public string? Reason { get; set; }
    }

    /// <summary>Khóa/xóa nhiều Shipper: 1–50 ID (ID trùng chỉ xử lý một lần), lý do bắt buộc ≤ 500 ký tự.</summary>
    public class ShipperBatchActionCommand
    {
        public List<Guid> ShipperIds { get; set; } = [];
        public string? Reason { get; set; }
    }

    public class UpdateShipperCommand
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public VehicleType VehicleType { get; set; } = VehicleType.Motorbike;
        public string? VehiclePlate { get; set; }
        /// <summary>true = Đang làm việc, false = Tạm nghỉ (không được phân công).</summary>
        public bool IsActive { get; set; } = true;
    }
}
