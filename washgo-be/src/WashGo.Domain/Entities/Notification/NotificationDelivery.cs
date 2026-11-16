using Volo.Abp.Domain.Entities;

namespace WashGo.Domain.Entities.Notification;

public class NotificationDelivery : Entity<Guid>
{
    public NotificationDelivery() { }
    public NotificationDelivery(Guid id) : base(id) { }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? LastError { get; set; }
}
