using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results;

public class AvailableShipperDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public VehicleType VehicleType { get; set; }
    public string? VehiclePlate { get; set; }
    public int ActiveDeliveryCount { get; set; }
    public decimal Rating { get; set; }
    public DeliveryTaskType TaskType { get; set; }
}
