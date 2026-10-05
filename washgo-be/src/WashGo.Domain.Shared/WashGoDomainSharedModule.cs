using Volo.Abp.Modularity;
using WashGo.Core.Domain.Shared;

namespace WashGo.Domain.Shared
{
    [DependsOn(
        typeof(WashGoCoreDomainSharedModule)
    )]
    public class WashGoDomainSharedModule : AbpModule
    {
    }
}
