using Volo.Abp.Domain.Entities;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Payment;

/// <summary>Durable query operation. No gateway call holds a database transaction open.</summary>
public class PaymentVerification : Entity<Guid>
{
    public PaymentVerification() { }
    public PaymentVerification(Guid id) : base(id) { }
    public Guid CustomerId { get; set; }
    public Guid OrderId { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? ParentOperationId { get; set; }
    public string State { get; set; } = "Queued";
    public bool IsActive { get; set; } = true;
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public string? ReasonCode { get; set; }
}
