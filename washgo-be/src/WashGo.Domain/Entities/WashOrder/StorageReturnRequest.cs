using Volo.Abp.Domain.Entities;
using WashGo.Domain.Shared.Enums;
namespace WashGo.Domain.Entities.WashOrder;
public class StorageReturnRequest : Entity<Guid>
{
    public StorageReturnRequest() { }
    public StorageReturnRequest(Guid id) : base(id) { }
    public Guid OrderId { get; set; }
    public Guid CycleId { get; set; }
    public Guid LockerId { get; set; }
    public StorageReturnState State { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime Deadline { get; set; }
    public decimal AcceptedFeeTotal { get; set; }
    public DateTime FeeAcceptedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
