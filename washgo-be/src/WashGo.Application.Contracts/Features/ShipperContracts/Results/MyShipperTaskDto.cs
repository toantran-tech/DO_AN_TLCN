using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class MyShipperTaskDto : ShipperTaskDto
{
    public Guid? StorageCycleId { get; set; }
    public string? StorageShopAddress { get; set; }
    public bool CanStorageCollect { get; set; }
    public bool CanStorageHandover { get; set; }
    public bool CanStorageDepart { get; set; }
    public bool CanStoragePlace { get; set; }
    public Guid AssignmentVersion => TaskId;
    public DeliveryTaskStatus TaskStatus => Status;
    public DeliveryTaskType? NextAction => CanCollect ? DeliveryTaskType.Collect : CanReturn ? DeliveryTaskType.Return : CanStorageCollect || CanStorageHandover ? DeliveryTaskType.StorageTransfer : CanStorageDepart || CanStoragePlace ? DeliveryTaskType.StorageReturn : null;
    public bool IsOverdue => Deadline.HasValue && ServerNow > Deadline.Value && (CanCollect || CanReturn || CanStorageCollect || CanStorageHandover || CanStorageDepart || CanStoragePlace);
    public long OrderRevision { get; set; }
    public string? TaskLockerName { get; set; }
    public string? TaskLockerAddress { get; set; }
    public double? TaskLockerLatitude { get; set; }
    public double? TaskLockerLongitude { get; set; }
    public List<string> TaskLockerImageUrls { get; set; } = [];
    public string? TaskBoxNumber { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? SlaStartedAt { get; set; }
    public int? SlaHours { get; set; }
    public string? SlaUnavailableReason { get; set; }
    public DateTime ServerNow { get; set; }
    public bool CanCollect { get; set; }
    public bool CanReturn { get; set; }
    public string? ReasonCode { get; set; }
    public string? MerchantName { get; set; }
    public string? Note { get; set; }
    public WashOrderDetailDto? Order { get; set; }
}
