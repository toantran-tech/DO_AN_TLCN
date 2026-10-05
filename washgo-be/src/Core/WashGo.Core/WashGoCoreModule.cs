using Volo.Abp.Application;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using WashGo.Core.Domain;
using WashGo.Core.Domain.Shared;

namespace WashGo.Core
{
    [DependsOn(
        typeof(WashGoCoreDomainModule),
        typeof(WashGoCoreDomainSharedModule),
        typeof(AbpDddApplicationModule),
        typeof(AbpEntityFrameworkCorePostgreSqlModule),
        typeof(AbpAspNetCoreMvcModule)
    )]
    public class WashGoCoreModule : AbpModule
    {
    }
}
