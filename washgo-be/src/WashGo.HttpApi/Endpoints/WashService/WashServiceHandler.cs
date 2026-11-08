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
            var group = app.MapGroup(_endpoint)
            .WithTags("Wash Service - Quản lý dịch vụ")
            // UC 3.5: only Admin manages services directly; Merchants use /merchant-service proposals (UC 2.6).
            .RequireAuthorization("AdminOnly");
            group.MapGet("/summary", async (IWashServiceAppService svc) => (await svc.GetSummaryAsync()).CustomResult())
                .WithSummary("Tổng dịch vụ và phân bố trạng thái duyệt/hoạt động, loại trừ xóa mềm.").WithOpenApi();

            group.MapPost("/filter", async (WashServiceFilterQuery request, IWashServiceAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách dịch vụ")
                .WithSummary("Admin xem dịch vụ, lọc theo Merchant, danh mục, trạng thái hoạt động và trạng thái duyệt.")
                .WithOpenApi();

            group.MapPost("/batch", async (WashGo.Application.Contracts.Features.AuditContracts.Commands.AdministrativeBatchCommand command, IWashServiceAppService svc) => (await svc.BatchAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Xử lý 1–50 dịch vụ: Delete hoặc Status đích; id/expectedVersion từng dòng, operationId cho toàn batch.").WithOpenApi();
            group.MapDelete("/{id:guid}", async (Guid id, string? expectedVersion, Guid? operationId, IWashServiceAppService svc) =>
                (await svc.DeleteAsync(id, expectedVersion, operationId)).CustomResult())
                .WithName("Xóa dịch vụ WashGo")
                .WithSummary("Xóa mềm dịch vụ; từ chối khi dịch vụ đang có đơn hàng chưa hoàn tất.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashServiceAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết dịch vụ")
                .WithSummary("Xem chi tiết dịch vụ.")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashServiceCommand command, IWashServiceAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới dịch vụ WashGo")
                .WithSummary("Thêm mới dịch vụ WashGo.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateWashServiceCommand command, IWashServiceAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật dịch vụ WashGo")
                .WithSummary("Cập nhật dịch vụ WashGo.")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashServiceStatusCommand command, IWashServiceAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái / duyệt dịch vụ")
                .WithSummary("Cập nhật trạng thái / duyệt dịch vụ.")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, WashServiceFilterQuery query, IWashServiceAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc dịch vụ")
                .WithSummary("Lấy giá trị distinct cho bộ lọc dịch vụ.")
                .WithOpenApi();
        }
    }
}
