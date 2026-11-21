using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results;

/// <summary>Shipper cho một chuyến cụ thể, kèm năng lực nhận nhiệm vụ do BE tính.</summary>
public class AvailableShipperDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    /// <summary>Ảnh đại diện của Shipper; null khi chưa có.</summary>
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public VehicleType VehicleType { get; set; }
    public string? VehiclePlate { get; set; }
    /// <summary>Giữ để tương thích; bằng activeTaskCount.</summary>
    public int ActiveDeliveryCount { get; set; }
    public decimal Rating { get; set; }
    public DeliveryTaskType TaskType { get; set; }
    /// <summary>Trạng thái vận hành (Available/Busy/Offline); không tự đổi khi số nhiệm vụ tăng.</summary>
    public ShipperStatus Status { get; set; }
    /// <summary>Tình trạng làm việc (false = tạm nghỉ).</summary>
    public bool IsWorking { get; set; }
    /// <summary>Số nhiệm vụ chưa hoàn tất đang tính vào giới hạn.</summary>
    public int ActiveTaskCount { get; set; }
    /// <summary>Giới hạn hiệu lực do BE quyết định (cài đặt hệ thống).</summary>
    public int MaxActiveTasks { get; set; }
    /// <summary>Số nhiệm vụ còn nhận được = max − active (không âm).</summary>
    public int RemainingTaskCapacity { get; set; }
    /// <summary>Có được phân công chuyến đang xét không (chính chuyến này không chiếm thêm chỗ khi phân công lại cùng người).</summary>
    public bool CanAssign { get; set; }
    /// <summary>AccountInactive | ShipperOnLeave | ShipperNotAvailable | ShipperCapacityExceeded; null khi chọn được.</summary>
    public string? BlockedReasonCode { get; set; }
}
