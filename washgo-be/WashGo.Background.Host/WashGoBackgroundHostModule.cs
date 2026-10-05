using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using WashGo.Application;
using WashGo.EntityFrameworkCore;

namespace WashGo.Background.Host
{
    [DependsOn(
        typeof(WashGoApplicationModule),
        typeof(WashGoEntityFrameworkCoreModule),
        typeof(AbpAutofacModule),
        typeof(AbpBackgroundWorkersModule)
    )]
    public class WashGoBackgroundHostModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpDbContextOptions>(options =>
            {
                options.UseNpgsql();
            });
        }

        public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            await context.AddBackgroundWorkerAsync<Workers.WashOrderSlaWorker>();
        }
    }
}
