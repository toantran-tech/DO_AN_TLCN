using Mapster;

namespace WashGo.Application.Mappings;

public static class WashGoMappingConfiguration
{
    public static TypeAdapterConfig Create()
    {
        var configuration = new TypeAdapterConfig { RequireExplicitMapping = true };
        configuration.Default.MaxDepth(32);
        configuration.Scan(typeof(WashGoApplicationModule).Assembly);
        configuration.Compile();
        return configuration;
    }
}
