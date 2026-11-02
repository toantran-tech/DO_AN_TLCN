using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;

namespace WashGo.HttpApi.Host.Workers
{
    /// <summary>
    /// Quét nhắc nhở theo thời gian định kỳ. Chạy trong tiến trình API để thông báo được đẩy
    /// real-time qua SignalR hub cùng tiến trình. Cấu hình: Reminders:Enabled, Reminders:IntervalSeconds.
    /// </summary>
    public class ReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReminderWorker> _logger;

        public ReminderWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ReminderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("Reminders:Enabled", true))
            {
                _logger.LogInformation("Reminder worker disabled by configuration.");
                return;
            }
            var interval = TimeSpan.FromSeconds(Math.Max(10, _configuration.GetValue("Reminders:IntervalSeconds", 60)));
            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IOrderReminderAppService>().RunAsync();
                    if (result.Data?.Total > 0)
                        _logger.LogInformation("Reminder worker sent {Total} reminders.", result.Data.Total);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // A failed scan is retried on the next tick; reminders already sent stay recorded.
                    _logger.LogError(ex, "Reminder scan failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
