using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using WashGo.Core.Domain.Shared;

namespace WashGo.Core.Domain
{
    [DependsOn(
        typeof(AbpDddDomainModule),
        typeof(WashGoCoreDomainSharedModule)
    )]
    public class WashGoCoreDomainModule : AbpModule
    {
    }
}
