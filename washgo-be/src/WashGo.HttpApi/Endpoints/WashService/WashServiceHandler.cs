using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Application.Contracts.Features.WashServiceContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.WashService
{
    public class WashServiceHandler : IEndpointBase
    {
        private const string _endpoint = "/wash-service";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Wash Service - Quản lý dịch vụ");

            group.MapPost("/filter", async (WashServiceFilterQuery request, IWashServiceAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách dịch vụ")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashServiceAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết dịch vụ")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashServiceCommand command, IWashServiceAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới dịch vụ WashGo")
                .WithOpenApi();

            group.MapPut("/", async (UpdateWashServiceCommand command, IWashServiceAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật dịch vụ WashGo")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashServiceStatusCommand command, IWashServiceAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái / duyệt dịch vụ")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, WashServiceFilterQuery query, IWashServiceAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc dịch vụ")
                .WithOpenApi();
        }
    }
}
