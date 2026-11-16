using Volo.Abp.Domain.Entities;
namespace WashGo.Domain.Entities.WashOrder;
public class StorageReferenceClaim : Entity<Guid>
{
    public string Code { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}
