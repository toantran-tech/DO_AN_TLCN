using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Locker
{
    public class LockerHandler : IEndpointBase
    {
        private const string _endpoint = "/locker";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Locker - Quản lý tủ thông minh")
            .RequireAuthorization("MerchantOrAdmin");

            group.MapPost("/filter", async (LockerFilterQuery request, ILockerAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách Locker")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, ILockerAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Locker")
                .WithOpenApi();

            group.MapPost("/", async (CreateLockerCommand command, ILockerAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới tủ Locker")
                .WithOpenApi();

            group.MapPut("/", async (UpdateLockerCommand command, ILockerAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật tủ Locker")
                .WithOpenApi();

            group.MapPut("/box-status", async (UpdateLockerBoxStatusCommand command, ILockerAppService svc) =>
                (await svc.UpdateBoxStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái ô tủ Locker")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, LockerFilterQuery query, ILockerAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc Locker")
                .WithOpenApi();
        }
    }
}
