using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WashGo.Application.Features.PaymentApplication.Services;

namespace WashGo.HttpApi.Host.Workers;

public class PaymentVerificationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<PaymentVerificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Payment:Verification:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPaymentVerificationRunner>().DrainVerificationAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Payment verification scan failed; durable leases will be retried.");
            }
        }
    }
}
