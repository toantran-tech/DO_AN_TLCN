using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

namespace WashGo.Application.Contracts.Features.LockerContracts.Results
{
    public class LockerResultDto
    {
        public Guid Id { get; set; }
        /// <summary>Các Merchant phụ trách locker, sắp theo độ ưu tiên.</summary>
        public List<LockerMerchantDto> Merchants { get; set; } = [];
        public Guid? ServiceAreaId { get; set; }
        public string? ServiceAreaName { get; set; }
        /// <summary>Ảnh locker theo thứ tự lưu (cả ở danh sách); thiếu ảnh trả [].</summary>
        public List<string> ImageUrls { get; set; } = [];
        /// <summary>Ảnh đầu tiên dùng làm thumbnail; null khi chưa có ảnh.</summary>
        public string? ThumbnailUrl => ImageUrls.Count > 0 ? ImageUrls[0] : null;
        public string? Description { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Ward { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public LockerStatus Status { get; set; }
        public LockerType LockerType { get; set; }
        public int TotalBoxes { get; set; }
        public int AvailableBoxes { get; set; }
        public int EmptyBoxesCount { get; set; }
        public long CreatedOn { get; set; }
        public List<LockerBoxResultDto> Boxes { get; set; } = [];
    }

    public class LockerMerchantDto
    {
        public Guid MerchantId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    public class LockerBoxResultDto
    {
        public Guid Id { get; set; }
        public Guid LockerId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize Size { get; set; }
        public LockerBoxStatus Status { get; set; }
        /// <summary>Ô đang có đồ (UC 2.5.2 highlight).</summary>
        public bool HasItems => Status == LockerBoxStatus.Occupied;
    }

    /// <summary>Một lượt sử dụng ô tủ (UC 2.5.3): khách gửi đồ hoặc Shipper trả đồ vào ô.</summary>
    public class LockerUsageDto
    {
        /// <summary>Định danh ổn định của lượt dùng: {orderId}:{usageType}.</summary>
        public string UsageId { get; set; } = string.Empty;
        /// <summary>Ô của lượt dùng (kể cả ô đã bị xóa sau đó).</summary>
        public Guid? BoxId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string BoxNumber { get; set; } = string.Empty;
        /// <summary>1 = khách gửi đồ vào ô, 2 = Shipper trả đồ đã giặt vào ô.</summary>
        public int UsageType { get; set; }
        /// <summary>Thời gian gửi (đồ được đặt vào ô).</summary>
        public DateTime? PutInAt { get; set; }
        /// <summary>Thời gian lấy (đồ được lấy ra khỏi ô); null nếu đồ còn trong ô.</summary>
        public DateTime? TakenOutAt { get; set; }
        public WashOrderStatus OrderStatus { get; set; }
    }
}

