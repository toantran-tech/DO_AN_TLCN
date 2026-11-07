using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

    [SevagoTable(WashGoDbProperties.Shippers, Schema = WashGoDbProperties.Schema)]
    /// <summary>Xóa = xóa mềm (ISoftDelete): ẩn khỏi danh sách/phân công, giữ liên kết đơn hàng và lịch sử giao nhận.</summary>
    public class Shipper : Entity<Guid>, ISoftDelete
    {
        public Shipper() { }
        public Shipper(Guid id) : base(id) { }

        public Guid UserId { get; set; }
        public Guid MerchantId { get; set; }
        public VehicleType VehicleType { get; set; } = VehicleType.Motorbike;
        public string? VehiclePlate { get; set; }
        public bool IsActive { get; set; } = true;
        public ShipperStatus Status { get; set; } = ShipperStatus.Available;
        public string? IdentityNumber { get; set; }
        /// <summary>Ảnh giấy tờ cũ dạng mảng, không rõ mặt trước/sau (giữ tương thích).</summary>
        public string? IdentityImages { get; set; }
        /// <summary>Ảnh mặt trước/mặt sau CMND/CCCD; luôn có đủ hai hoặc không có.</summary>
        public string? IdentityFrontImageUrl { get; set; }
        public string? IdentityBackImageUrl { get; set; }
        public int TotalDeliveries { get; set; }
        public decimal Rating { get; set; }
        /// <summary>Thời điểm và lý do khóa tài khoản (User.IsActive = false) gần nhất; null khi không bị khóa.</summary>
        public DateTime? LockedAt { get; set; }
        public string? LockReason { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
