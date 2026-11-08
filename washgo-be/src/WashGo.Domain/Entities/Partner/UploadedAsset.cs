using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Partner;

public class UploadedAsset : Entity<Guid>
{
    public UploadedAsset() { }
    public UploadedAsset(Guid id) : base(id) { }
    public Guid OwnerId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReferencedAt { get; set; }
    public bool IsDeleted { get; set; }
}
