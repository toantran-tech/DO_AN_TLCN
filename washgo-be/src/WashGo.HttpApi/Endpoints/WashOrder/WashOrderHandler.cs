using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.WashOrder
{
    public class WashOrderHandler : IEndpointBase
    {
        private const string _endpoint = "/wash-order";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Wash Order - Quản lý đơn hàng");

            group.MapPost("/filter", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách đơn WashGo")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết đơn WashGo")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Tạo đơn hàng WashGo mới")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashOrderStatusCommand command, IWashOrderAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái đơn")
                .WithOpenApi();

            group.MapPut("/assign-shipper", async (AssignShipperCommand command, IWashOrderAppService svc) =>
                (await svc.AssignShipperAsync(command)).CustomResult())
                .WithName("Phân công Shipper cho đơn")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, WashOrderFilterQuery query, IWashOrderAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc cột đơn hàng")
                .WithOpenApi();
        }
    }
}
