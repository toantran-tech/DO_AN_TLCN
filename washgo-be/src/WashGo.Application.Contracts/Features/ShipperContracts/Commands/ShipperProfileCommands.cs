using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Commands
{
    /// <summary>UC 4.1.4: các trường Shipper được tự sửa; Email và Merchant phụ trách không đổi.</summary>
    public class UpdateShipperProfileCommand
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        /// <summary>Ảnh đã upload qua /files/images; null = giữ nguyên, chuỗi rỗng = xóa avatar.</summary>
        public string? AvatarUrl { get; set; }
        public VehicleType VehicleType { get; set; } = VehicleType.Motorbike;
        public string? VehiclePlate { get; set; }
    }
}
