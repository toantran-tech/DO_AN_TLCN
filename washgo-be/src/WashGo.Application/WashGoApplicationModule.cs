using WashGo.Application.Features.AuthApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Modularity;
using WashGo.Application.Contracts;
using WashGo.Core;
using WashGo.Domain;
using WashGo.Application.Features.ChatbotApplication.Providers;
using WashGo.Application.Features.ChatbotApplication.Services;
using WashGo.Application.Mappings;

namespace WashGo.Application
{
    [DependsOn(
        typeof(WashGoDomainModule),
        typeof(WashGoApplicationContractsModule),
        typeof(AbpDddApplicationModule),
        typeof(AbpObjectMappingModule),
        typeof(WashGoCoreModule)
    )]
    public class WashGoApplicationModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddWashGoObjectMapping();
            context.Services.AddHttpClient(BrevoEmailClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            context.Services.AddHttpClient(ObjectStorage.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(120))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            context.Services.AddHttpClient<IChatModelClient, GeminiChatModelClient>(client =>
                client.Timeout = TimeSpan.FromSeconds(65));

        }
    }
}
