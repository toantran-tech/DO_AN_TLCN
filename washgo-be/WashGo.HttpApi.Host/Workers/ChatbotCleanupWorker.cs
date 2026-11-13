using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;
using WashGo.Domain.Entities.Chatbot.Interfaces;

namespace WashGo.HttpApi.Host.Workers;

public class ChatbotCleanupWorker(IServiceScopeFactory scopes, ILogger<ChatbotCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
                await scope.ServiceProvider.GetRequiredService<IChatConversationRepository>().PurgeAsync(DateTime.UtcNow.AddDays(-90));
                await uow.CompleteAsync();
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError("Chatbot retention cleanup failed; retry on next scan."); }
        }
    }
}
