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
        public bool IsActive { get; set; }
        public int TotalDeliveries { get; set; }
        public int ActiveTaskCount { get; set; }
        public decimal Rating { get; set; }
        /// <summary>false khi Rating = 0 (chưa có đánh giá); khi sắp xếp theo Rating các Shipper này luôn đứng cuối.</summary>
        public bool HasRating => Rating > 0;
        public string? IdentityNumber { get; set; }
        public List<string> IdentityImageUrls { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }
}
