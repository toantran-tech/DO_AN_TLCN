using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries;

public class MyShipperTaskQuery : BaseFilterQuery
{
    public MyShipperTaskQuery() { Page = 1; Take = 20; }
    public DeliveryTaskType? TaskType { get; set; }
    public DeliveryTaskStatus? TaskStatus { get; set; } = DeliveryTaskStatus.Assigned;
    public long AssignedFrom { get; set; }
    public long AssignedTo { get; set; }
}
