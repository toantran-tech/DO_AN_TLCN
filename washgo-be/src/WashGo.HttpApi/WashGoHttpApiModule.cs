using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;
using WashGo.Application.Contracts;
using WashGo.Core;
using WashGo.Core.Endpoint;
using WashGo.HttpApi.Endpoints.Auth;
using WashGo.HttpApi.Endpoints.Locker;
using WashGo.HttpApi.Endpoints.Merchant;
using WashGo.HttpApi.Endpoints.ServiceArea;
using WashGo.HttpApi.Endpoints.WashOrder;
using WashGo.HttpApi.Endpoints.WashService;

namespace WashGo.HttpApi
{
    [DependsOn(
        typeof(WashGoApplicationContractsModule),
        typeof(WashGoCoreModule),
        typeof(AbpAspNetCoreMvcModule)
    )]
    public class WashGoHttpApiModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient<IEndpointBase, AuthHandler>();
            context.Services.AddTransient<IEndpointBase, WashOrderHandler>();
            context.Services.AddTransient<IEndpointBase, LockerHandler>();
            context.Services.AddTransient<IEndpointBase, WashServiceHandler>();
            context.Services.AddTransient<IEndpointBase, ServiceAreaHandler>();
            context.Services.AddTransient<IEndpointBase, MerchantHandler>();
        }
    }
}
