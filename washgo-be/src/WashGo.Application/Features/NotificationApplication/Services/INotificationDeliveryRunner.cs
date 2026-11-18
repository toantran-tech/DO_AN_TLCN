namespace WashGo.Application.Features.NotificationApplication.Services;
public interface INotificationDeliveryRunner
{
    /// <summary>Handles one batch of due deliveries and returns how many it claimed or suppressed.</summary>
    Task<int> DrainAsync();
}
