using Volo.Abp.Domain.Entities;
using WashGo.Domain.Shared.Enums;
namespace WashGo.Domain.Entities.WashOrder;
public class OrderStorageCycle : Entity<Guid>
{
    public OrderStorageCycle() { }
    public OrderStorageCycle(Guid id) : base(id) { }
    public Guid OrderId { get; set; }
    public Guid MerchantId { get; set; }
    public StorageCycleState State { get; set; }
    public string Basis { get; set; } = "Captured";
    public string ShopAddress { get; set; } = string.Empty;
    public Guid? SourceLockerId { get; set; }
    public Guid? SourceBoxId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? IntakeAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? TransferDeadline { get; set; }
    public decimal LockerFee { get; set; }
    public decimal WarehouseFee { get; set; }
    public Guid? IntakeActorId { get; set; }
    public string? IntakeReceipt { get; set; }
    public string? ExceptionNote { get; set; }
}
