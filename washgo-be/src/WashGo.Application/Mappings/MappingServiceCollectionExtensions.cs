using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.ObjectMapping;

namespace WashGo.Application.Mappings;

internal static class MappingServiceCollectionExtensions
{
    public static void AddWashGoObjectMapping(this IServiceCollection services)
    {
        services.AddSingleton(WashGoMappingConfiguration.Create());
        services.Replace(ServiceDescriptor.Transient<IAutoObjectMappingProvider, MapsterObjectMappingProvider>());
        services.AddTransient<IAutoObjectMappingProvider<WashGoApplicationModule>, MapsterObjectMappingProvider>();
    }
}
