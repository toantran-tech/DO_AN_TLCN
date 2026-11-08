using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Audit;

/// <summary>Durable result of an explicitly keyed administrative write; stored in the business transaction.</summary>
public class AdministrativeOperation : Entity<Guid>
{
    public AdministrativeOperation() { }
    public AdministrativeOperation(Guid id) : base(id) { }
    public Guid ActorId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
