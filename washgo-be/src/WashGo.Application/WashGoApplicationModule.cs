using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using WashGo.Application.Contracts;
using WashGo.Core;
using WashGo.Domain;

namespace WashGo.Application
{
    [DependsOn(
        typeof(WashGoDomainModule),
        typeof(WashGoApplicationContractsModule),
        typeof(AbpDddApplicationModule),
        typeof(AbpAutoMapperModule),
        typeof(WashGoCoreModule)
    )]
    public class WashGoApplicationModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<WashGoApplicationModule>();
            });
        }
    }
}
