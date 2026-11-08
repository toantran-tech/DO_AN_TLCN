using Volo.Abp.Domain.Entities;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner;

public class PrivateDocument : Entity<Guid>
{
    public PrivateDocument() { }
    public PrivateDocument(Guid id) : base(id) { }
    public Guid OwnerId { get; set; }
    public Guid? MerchantId { get; set; }
    public MerchantDocumentKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>First reference by a profile or update request. Referenced history is retained.</summary>
    public DateTime? ReferencedAt { get; set; }
    public bool IsDeleted { get; set; }
}
