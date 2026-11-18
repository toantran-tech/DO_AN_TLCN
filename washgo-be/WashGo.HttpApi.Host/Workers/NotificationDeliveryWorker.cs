using WashGo.Application.Features.NotificationApplication.Services;
namespace WashGo.HttpApi.Host.Workers;
public class NotificationDeliveryWorker(IServiceScopeFactory scopes, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // A full batch means more due rows are waiting: keep draining so a burst is not throttled to one batch per tick.
                // Every full batch moved 40 rows out of the due set, so the loop ends once the backlog is cleared.
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    if (await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().DrainAsync() < NotificationDeliveryRunner.BatchSize) break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Notification outbox scan failed; durable leases will recover."); }
        }
    }
}
