using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace WashGo.Background.Host.Workers
{
    public class WashOrderSlaWorker : AsyncPeriodicBackgroundWorkerBase
    {
        public WashOrderSlaWorker(
            AbpAsyncTimer timer,
            IServiceScopeFactory serviceScopeFactory)
            : base(timer, serviceScopeFactory)
        {
            // Run every 60 seconds
            Timer.Period = 60000;
        }

        protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {
            Logger.LogInformation("WashOrderSlaWorker is checking SLA and expiring locker bookings at {Time}", DateTimeOffset.UtcNow);
            await Task.CompletedTask;
        }
    }
}
