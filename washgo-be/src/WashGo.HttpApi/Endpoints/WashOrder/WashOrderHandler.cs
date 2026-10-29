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
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Lọc danh sách đơn WashGo")
                .WithOpenApi();

            group.MapPost("/mine", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetMyOrdersAsync(request)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem danh sách đơn hàng của tôi")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Xem chi tiết đơn WashGo")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo đơn hàng WashGo mới")
                .WithOpenApi();

            group.MapPost("/{id:guid}/cancel", async (Guid id, CancelWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CancelAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng hủy đơn của mình")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashOrderStatusCommand command, IWashOrderAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Cập nhật trạng thái đơn")
                .WithOpenApi();

            group.MapPut("/{id:guid}/actual-weight", async (Guid id, UpdateActualWeightCommand command, IWashOrderAppService svc) =>
                (await svc.UpdateActualWeightAsync(id, command)).CustomResult())
                .RequireAuthorization("MerchantOnly")
                .WithName("Merchant cập nhật cân nặng thực tế và chốt giá")
                .WithOpenApi();

            group.MapPost("/move-to-storage", async (MoveOrderToStorageCommand command, IWashOrderAppService svc) =>
                (await svc.MoveToStorageAsync(command)).CustomResult())
                .RequireAuthorization("ShipperOrAdmin")
                .WithName("Ghi nhận chuyển đồ quá hạn sang kho")
                .WithOpenApi();

            group.MapPut("/assign-shipper", async (AssignShipperCommand command, IWashOrderAppService svc) =>
                (await svc.AssignShipperAsync(command)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Phân công Shipper cho đơn")
                .WithOpenApi();

            group.MapGet("/{id:guid}/available-shippers", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetAvailableShippersAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Xem Shipper khả dụng cho đơn")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, WashOrderFilterQuery query, IWashOrderAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Lấy giá trị distinct cho bộ lọc cột đơn hàng")
                .WithOpenApi();
        }
    }
}
