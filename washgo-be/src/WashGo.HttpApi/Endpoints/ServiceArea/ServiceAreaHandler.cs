using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.ServiceArea
{
    public class ServiceAreaHandler : IEndpointBase
    {
        private const string _endpoint = "/service-area";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Service Area - Quản lý vùng phục vụ");

            group.MapPost("/filter", async (ServiceAreaFilterQuery request, IServiceAreaAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách vùng phục vụ")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IServiceAreaAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết vùng phục vụ")
                .WithOpenApi();

            group.MapPost("/", async (CreateServiceAreaCommand command, IServiceAreaAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới vùng phục vụ")
                .WithOpenApi();

            group.MapPut("/", async (UpdateServiceAreaCommand command, IServiceAreaAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật vùng phục vụ")
                .WithOpenApi();

            group.MapPatch("/{id:guid}/toggle-active", async (Guid id, IServiceAreaAppService svc) =>
                (await svc.ToggleActiveAsync(id)).CustomResult())
                .WithName("Bật / Tắt trạng thái vùng phục vụ")
                .WithOpenApi();
        }
    }
}
