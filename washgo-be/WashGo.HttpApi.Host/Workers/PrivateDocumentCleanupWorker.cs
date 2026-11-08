using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WashGo.Application.Features.FileApplication.Services;

namespace WashGo.HttpApi.Host.Workers;

public class PrivateDocumentCleanupWorker(IServiceScopeFactory scopes, ILogger<PrivateDocumentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PrivateDocumentStore>().CleanupExpiredAsync();
                await scope.ServiceProvider.GetRequiredService<UploadedAssetRegistry>().CleanupAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(ex, "Private document cleanup failed; retry on next scan."); }
        }
    }
}
