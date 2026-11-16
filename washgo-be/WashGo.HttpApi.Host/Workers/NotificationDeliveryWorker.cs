using WashGo.Application.Features.NotificationApplication.Services;
namespace WashGo.HttpApi.Host.Workers;
public class NotificationDeliveryWorker(IServiceScopeFactory scopes, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().DrainAsync(); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Notification outbox scan failed; durable leases will recover."); }
        }
    }
}
