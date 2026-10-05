using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using WashGo.Core;
using WashGo.Domain;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.EntityFrameworkCore.Repositories;

namespace WashGo.EntityFrameworkCore
{
    [DependsOn(
        typeof(WashGoDomainModule),
        typeof(WashGoCoreModule),
        typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
    public class WashGoEntityFrameworkCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAbpDbContext<WashGoDbContext>(options =>
            {
                options.AddDefaultRepositories(includeAllEntities: true);
                options.AddRepository<WashGo.Domain.Entities.WashOrder.WashOrder, WashOrderRepository>();
                options.AddRepository<WashGo.Domain.Entities.Locker.Locker, LockerRepository>();
                options.AddRepository<WashGo.Domain.Entities.WashService.WashService, WashServiceRepository>();
            });

            context.Services.AddTransient<IWashOrderRepository, WashOrderRepository>();
            context.Services.AddTransient<ILockerRepository, LockerRepository>();
            context.Services.AddTransient<IWashServiceRepository, WashServiceRepository>();
        }
    }
}
