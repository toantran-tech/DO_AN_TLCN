using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Merchant
{
    public class MerchantHandler : IEndpointBase
    {
        private const string _endpoint = "/merchant";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Merchant - Quản lý đối tác giặt sấy");

            group.MapPost("/filter", async (MerchantFilterQuery request, IMerchantAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách đối tác giặt sấy")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IMerchantAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết đối tác giặt sấy")
                .WithOpenApi();

            group.MapPost("/", async (CreateMerchantCommand command, IMerchantAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới hồ sơ đối tác giặt sấy")
                .WithOpenApi();

            group.MapPut("/", async (UpdateMerchantCommand command, IMerchantAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật hồ sơ đối tác giặt sấy")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateMerchantStatusCommand command, IMerchantAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái đối tác giặt sấy")
                .WithOpenApi();
        }
    }
}
