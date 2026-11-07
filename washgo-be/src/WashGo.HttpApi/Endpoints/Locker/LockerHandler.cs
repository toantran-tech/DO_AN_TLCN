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

            group.MapGet("/summary", async (Guid? merchantId, ILockerAppService svc) =>
                (await svc.GetSummaryAsync(merchantId)).CustomResult())
                .WithName("Thống kê Locker")
                .WithSummary("Tổng locker được gán (đang hoạt động/bảo trì/ngừng) và tổng ô chưa xóa theo trạng thái (trống/có đồ/giữ chỗ/bảo trì), một lần đếm, không theo bộ lọc bảng. Merchant theo JWT; merchantId chỉ dành cho Admin.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, ILockerAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Locker")
                .WithSummary("Chi tiết locker và trạng thái từng ô (Trống/Đang giữ/Đang có đồ/Bảo trì, cờ hasItems); Merchant chỉ xem locker của mình.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/usage-history", async (Guid id, long? fromDate, long? toDate, Guid? boxId, int? page, int? take, ILockerAppService svc) =>
            {
                var result = await svc.GetUsageHistoryAsync(id, new LockerUsageQuery
                {
                    FromDate = fromDate, ToDate = toDate, BoxId = boxId, Page = page ?? 1, Take = take ?? 20
                });
                // Paged results map every failure to 400; a locker outside the caller's scope is 404.
                return result.StatusCode == System.Net.HttpStatusCode.NotFound ? Results.NotFound(result) : result.CustomResult();
            })
                .WithName("Lịch sử sử dụng Locker")
                .WithSummary("Các lượt dùng ô (usageId, boxId, mã đơn, ô, gửi/trả, thời gian đặt vào/lấy ra, trạng thái đơn); lọc theo thời gian đặt vào và theo ô. Merchant chỉ thấy lượt của đơn thuộc tiệm mình (locker dùng chung). Khoảng ngày sai hoặc boxId không thuộc locker → 400.")
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

            group.MapDelete("/{id:guid}", async (Guid id, ILockerAppService svc) =>
                (await svc.DeleteAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xóa Locker")
                .WithSummary("Xóa mềm locker và các ô tủ; từ chối khi locker còn đơn hàng chưa xử lý.")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, LockerFilterQuery query, ILockerAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc Locker")
                .WithSummary("Lấy giá trị distinct cho bộ lọc Locker.")
                .WithOpenApi();
        }
    }
}
