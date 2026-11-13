using WashGo.Domain.Shared.Enums;
namespace WashGo.Application.Contracts.Features.StorageContracts.Results;
public class StorageCycleDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public long OrderRevision { get; set; }
    public StorageCycleState State { get; set; }
    public string Basis { get; set; } = string.Empty;
    public string? ShopAddress { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? IntakeAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public string? ReceiptReference { get; set; }
    public string? ExceptionNote { get; set; }
    public DateTime? TransferDeadline { get; set; }
    public Guid? ReturnRequestId { get; set; }
    public StorageReturnState? ReturnState { get; set; }
    public Guid? ReturnLockerId { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public decimal FrozenLockerFee { get; set; }
    public decimal FrozenWarehouseFee { get; set; }
    public decimal FeeTotal { get; set; }
    public decimal FeePaid { get; set; }
    public decimal Outstanding => Math.Max(0, FeeTotal - FeePaid);
    public Guid? TaskId { get; set; }
    public bool CanAssign { get; set; }
    public bool CanCancelAssignment { get; set; }
    public bool CanRequestReturn { get; set; }
    public bool CanCancelReturn { get; set; }
    public bool CanCollect { get; set; }
    public bool CanHandover { get; set; }
    public bool CanDepart { get; set; }
    public string? DepartBlockedReason { get; set; }
    public bool CanPlace { get; set; }
    public bool IsLate { get; set; }
}
