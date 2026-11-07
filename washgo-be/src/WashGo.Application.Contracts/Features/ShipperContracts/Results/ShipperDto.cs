using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    public class ShipperDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid MerchantId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Email { get; set; } = string.Empty;
        /// <summary>Ảnh đại diện (User.Avatar); null khi chưa có. Không phải ảnh giấy tờ.</summary>
        public string? AvatarUrl { get; set; }
        public VehicleType VehicleType { get; set; }
        public string? VehiclePlate { get; set; }
        public ShipperStatus Status { get; set; }
        /// <summary>Đang làm việc (true) / Tạm nghỉ (false). Khác với khóa tài khoản.</summary>
        public bool IsActive { get; set; }
        /// <summary>Tài khoản đăng nhập còn hiệu lực (User.IsActive).</summary>
        public bool AccountActive { get; set; }
        /// <summary>Tài khoản bị khóa: không đăng nhập, phiên cũ hết hiệu lực, không được phân công.</summary>
        public bool IsLocked => !AccountActive;
        public DateTime? LockedAt { get; set; }
        public string? LockReason { get; set; }
        public int TotalDeliveries { get; set; }
        public int ActiveTaskCount { get; set; }
        public decimal Rating { get; set; }
        /// <summary>false khi Rating = 0 (chưa có đánh giá); khi sắp xếp theo Rating các Shipper này luôn đứng cuối.</summary>
        public bool HasRating => Rating > 0;
        public string? IdentityNumber { get; set; }
        /// <summary>Tương thích cũ: [mặt trước, mặt sau] khi đã rõ mặt, nếu không là mảng ảnh cũ.</summary>
        public List<string> IdentityImageUrls { get; set; } = [];
        public string? IdentityFrontImageUrl { get; set; }
        public string? IdentityBackImageUrl { get; set; }
        /// <summary>None (chưa có ảnh), FrontBack (đủ hai mặt), Unspecified (ảnh cũ chưa xác định mặt, cần cập nhật lại).</summary>
        public string IdentityImageSides { get; set; } = "None";
        public DateTime CreatedAt { get; set; }
        /// <summary>Phiên bản dữ liệu; gửi lại làm expectedRevision khi sửa.</summary>
        public long Revision { get; set; }
        /// <summary>Kết quả gửi email đăng nhập: 0 Unknown, 1 Sent, 2 Failed (không bao giờ trả mật khẩu).</summary>
        public CredentialDeliveryStatus CredentialDelivery { get; set; }
        public bool CredentialEmailSent => CredentialDelivery == CredentialDeliveryStatus.Sent;
        public DateTime? CredentialSentAt { get; set; }
    }
}
