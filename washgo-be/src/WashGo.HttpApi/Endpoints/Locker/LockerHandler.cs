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
                .WithSummary("Admin xem mọi locker; Merchant chỉ xem locker được gán (tên, địa chỉ, số ô, số ô trống, trạng thái).")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, ILockerAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Locker")
                .WithSummary("Chi tiết locker và trạng thái từng ô (Trống/Đang giữ/Đang có đồ/Bảo trì, cờ hasItems); Merchant chỉ xem locker của mình.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/usage-history", async (Guid id, long? fromDate, long? toDate, int? page, int? take, ILockerAppService svc) =>
            {
                var result = await svc.GetUsageHistoryAsync(id, new LockerUsageQuery
                {
                    FromDate = fromDate, ToDate = toDate, Page = page ?? 1, Take = take ?? 20
                });
                // Paged results map every failure to 400; a locker outside the caller's scope is 404.
                return result.StatusCode == System.Net.HttpStatusCode.NotFound ? Results.NotFound(result) : result.CustomResult();
            })
                .WithName("Lịch sử sử dụng Locker")
                .WithSummary("Các lượt dùng ô: mã đơn, ô, gửi/trả, thời gian đặt vào, thời gian lấy ra, trạng thái đơn; lọc theo thời gian.")
                .WithOpenApi();

            group.MapPost("/", async (CreateLockerCommand command, ILockerAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Thêm mới tủ Locker")
                .WithSummary("Thêm mới tủ Locker.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateLockerCommand command, ILockerAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Cập nhật tủ Locker")
                .WithSummary("Cập nhật tủ Locker.")
                .WithOpenApi();

            group.MapPut("/box-status", async (UpdateLockerBoxStatusCommand command, ILockerAppService svc) =>
                (await svc.UpdateBoxStatusAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Cập nhật trạng thái ô tủ Locker")
                .WithSummary("Cập nhật trạng thái ô tủ Locker.")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, LockerFilterQuery query, ILockerAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc Locker")
                .WithSummary("Lấy giá trị distinct cho bộ lọc Locker.")
                .WithOpenApi();
        }
    }
}
