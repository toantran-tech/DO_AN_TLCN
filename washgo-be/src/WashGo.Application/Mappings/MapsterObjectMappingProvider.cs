using Mapster;
using Volo.Abp.ObjectMapping;

namespace WashGo.Application.Mappings;

public sealed class MapsterObjectMappingProvider(TypeAdapterConfig configuration)
    : IAutoObjectMappingProvider, IAutoObjectMappingProvider<WashGoApplicationModule>
{
    public TDestination Map<TSource, TDestination>(object source) =>
        TypeAdapter.Adapt<TSource, TDestination>((TSource)source, configuration);

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) =>
        TypeAdapter.Adapt(source, destination, configuration);
}
