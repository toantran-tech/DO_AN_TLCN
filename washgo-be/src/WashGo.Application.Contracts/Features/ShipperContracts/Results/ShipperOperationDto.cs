using WashGo.Application.Contracts.Features.WashOrderContracts.Results;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperOperationDto
{
    public Guid OperationId { get; set; }
    public Guid TaskId { get; set; }
    public string State { get; set; } = "unknown";
    public WashOrderDetailDto? Result { get; set; }
}
