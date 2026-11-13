using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Commands;

public class UpdateShipperWorkStatusCommand
{
    public ShipperStatus Status { get; set; }
    public string? ExpectedRevision { get; set; }
    public Guid? OperationId { get; set; }
}
