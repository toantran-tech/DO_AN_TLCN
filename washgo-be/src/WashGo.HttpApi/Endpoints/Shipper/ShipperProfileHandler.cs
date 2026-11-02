using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Shipper
{
    public class ShipperProfileHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/shipper-profile")
                .WithTags("Shipper Profile - Hồ sơ Shipper")
                .RequireAuthorization("ShipperOnly");

            group.MapGet("/", async (IShipperProfileAppService svc) =>
                (await svc.GetMyProfileAsync()).CustomResult())
                .WithName("Shipper xem hồ sơ")
                .WithSummary("Họ tên, email, SĐT, avatar, phương tiện, biển số, trạng thái, tổng đơn đã giao, đánh giá, Merchant phụ trách.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateShipperProfileCommand command, IShipperProfileAppService svc) =>
                (await svc.UpdateMyProfileAsync(command)).CustomResult())
                .WithName("Shipper cập nhật hồ sơ")
                .WithSummary("Sửa họ tên, SĐT, avatar, phương tiện, biển số; email và Merchant phụ trách không đổi.")
                .WithOpenApi();
        }
    }
}
