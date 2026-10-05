using Microsoft.AspNetCore.Routing;
using Volo.Abp.DependencyInjection;

namespace WashGo.Core.Endpoint
{
    public interface IEndpointBase : ITransientDependency
    {
        void MapEndpoint(IEndpointRouteBuilder app);
    }
}
