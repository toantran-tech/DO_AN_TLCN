using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using WashGo.Core;
using WashGo.Core.Domain;
using WashGo.Domain.Shared;

namespace WashGo.Domain
{
    [DependsOn(
        typeof(WashGoCoreModule),
        typeof(WashGoCoreDomainModule),
        typeof(WashGoDomainSharedModule),
        typeof(AbpDddDomainModule)
    )]
    public class WashGoDomainModule : AbpModule
    {
    }
}
