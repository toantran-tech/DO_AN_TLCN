using System;
using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Partner;

/// <summary>Committed Admin Merchant writes; stores the original response for safe retry.</summary>
public class MerchantWriteOperation : Entity<Guid>
{
    public MerchantWriteOperation() { }
    public MerchantWriteOperation(Guid id) : base(id) { }
    public Guid ActorId { get; set; }
    public Guid MerchantId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
