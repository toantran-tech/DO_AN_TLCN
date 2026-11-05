using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.CustomerContracts.Commands;
using WashGo.Application.Contracts.Features.CustomerContracts.Queries;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Customer;

public class CustomerHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customer").WithTags("Customer - Hồ sơ khách hàng")
            .RequireAuthorization("CustomerOnly");

        group.MapGet("/me", async (ICustomerAppService service) =>
            (await service.GetMyProfileAsync()).CustomResult())
            .WithName("Xem hồ sơ Customer của tôi")
            .WithSummary("Xem hồ sơ Customer của tôi.")
            .WithOpenApi();

        group.MapPut("/me", async (UpdateCustomerProfileCommand command, ICustomerAppService service) =>
            (await service.UpdateMyProfileAsync(command)).CustomResult())
            .WithName("Cập nhật hồ sơ Customer của tôi")
            .WithSummary("Cập nhật hồ sơ: bắt buộc họ tên, số điện thoại 10 số bắt đầu bằng 0, địa chỉ (tối đa 500 ký tự), tỉnh/thành phố (tối đa 100 ký tự).")
            .WithOpenApi();

        group.MapPost("/services/filter", async (CustomerServiceFilterQuery query, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServicesAsync(query)).CustomResult())
            .WithName("Customer lọc và sắp xếp dịch vụ")
            .WithSummary("Lọc bằng CustomerServiceFilterQuery kế thừa BaseFilterQuery: keyword, page (từ 1), take, sortBy (Field=Name|UnitPrice, IsDescending), serviceType, minPrice/maxPrice. Nhiều tiêu chí sort theo thứ tự trong mảng.")
            .WithOpenApi();

        group.MapGet("/services/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServiceAsync(id)).CustomResult())
            .WithName("Customer xem chi tiết dịch vụ đang bán")
            .WithSummary("Customer xem chi tiết dịch vụ đang bán.")
            .WithOpenApi();

        group.MapGet("/lockers", async (string? city, string? district, string? keyword,
            int? page, int? take, double? latitude, double? longitude, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockersAsync(city, district, keyword, page ?? 1, take ?? 20, latitude, longitude)).CustomResult())
            .WithName("Customer tìm tủ còn ô trống")
            .WithSummary("Customer tìm tủ còn ô trống; gửi latitude/longitude của khách để nhận khoảng cách (km) và sắp xếp gần nhất trước.")
            .WithOpenApi();

        group.MapGet("/lockers/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockerAsync(id)).CustomResult())
            .WithName("Customer xem các ô tủ còn trống")
            .WithSummary("Customer xem các ô tủ còn trống.")
            .WithOpenApi();
    }
}
