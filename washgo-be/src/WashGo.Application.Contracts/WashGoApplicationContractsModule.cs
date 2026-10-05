using Volo.Abp.Application;
using Volo.Abp.Modularity;
using WashGo.Core.Domain.Shared;
using WashGo.Domain.Shared;

namespace WashGo.Application.Contracts
{
    [DependsOn(
        typeof(WashGoCoreDomainSharedModule),
        typeof(WashGoDomainSharedModule),
        typeof(AbpDddApplicationContractsModule)
    )]
    public class WashGoApplicationContractsModule : AbpModule
    {
    }
}
