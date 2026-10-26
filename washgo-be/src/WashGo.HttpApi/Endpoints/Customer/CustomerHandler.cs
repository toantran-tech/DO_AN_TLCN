using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.CustomerContracts;
using WashGo.Core.Endpoint;
using WashGo.Domain.Shared.Enums;

namespace WashGo.HttpApi.Endpoints.Customer;

public class CustomerHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customer").WithTags("Customer - Hồ sơ khách hàng")
            .RequireAuthorization("CustomerOnly");

        group.MapGet("/me", async (ICustomerAppService service) =>
            (await service.GetMyProfileAsync()).CustomResult())
            .WithName("Xem hồ sơ Customer của tôi").WithOpenApi();

        group.MapPut("/me", async (UpdateCustomerProfileCommand command, ICustomerAppService service) =>
            (await service.UpdateMyProfileAsync(command)).CustomResult())
            .WithName("Cập nhật hồ sơ Customer của tôi").WithOpenApi();

        group.MapGet("/services", async (Guid? merchantId, WashServiceType? serviceType, string? keyword,
            int? page, int? take, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServicesAsync(merchantId, serviceType, keyword, page ?? 1, take ?? 20)).CustomResult())
            .WithName("Customer xem dịch vụ đang bán").WithOpenApi();

        group.MapGet("/services/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServiceAsync(id)).CustomResult())
            .WithName("Customer xem chi tiết dịch vụ đang bán").WithOpenApi();

        group.MapGet("/lockers", async (string? city, string? district, string? keyword,
            int? page, int? take, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockersAsync(city, district, keyword, page ?? 1, take ?? 20)).CustomResult())
            .WithName("Customer tìm tủ còn ô trống").WithOpenApi();

        group.MapGet("/lockers/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockerAsync(id)).CustomResult())
            .WithName("Customer xem các ô tủ còn trống").WithOpenApi();
    }
}
