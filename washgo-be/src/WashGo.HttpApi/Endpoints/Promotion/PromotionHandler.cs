using System;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.PromotionContracts.Commands;
using WashGo.Application.Contracts.Features.PromotionContracts.Queries;
using WashGo.Application.Contracts.Features.PromotionContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Promotion
{
    public class PromotionHandler : IEndpointBase
    {
        private const string _endpoint = "/promotion";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Promotion - Quản lý khuyến mãi")
            .RequireAuthorization("AdminOnly");
            group.MapGet("/summary", async (IPromotionAppService svc) => (await svc.GetSummaryAsync()).CustomResult())
                .WithSummary("Thống kê mã giảm giá theo trạng thái từ DB tại thời điểm generatedAt.").WithOpenApi();
            group.MapPost("/{id:guid}/usages/filter", async (Guid id, PromotionUsageFilterQuery query, IPromotionAppService svc) =>
                (await svc.GetUsageHistoryAsync(id, query)).CustomResult())
                .WithSummary("Admin xem lịch sử dùng/hoàn lượt voucher; lọc orderId/customerId/isReverted, fromDate/toDate theo UsedAt (Unix giây); phân trang và sort UsedAt/RevertedAt/DiscountAmount/OrderAmount/Id. Lý do hoàn lượt cũ chưa lưu trả null.")
                .WithOpenApi();

            group.MapPost("/filter", async (PromotionFilterQuery request, IPromotionAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách mã giảm giá")
                .WithSummary("Lọc mã theo trạng thái, loại giảm, khoảng hiệu lực; sortBy nhiều tiêu chí Code/CreatedOn/StartAt/EndAt/UsageLimit/UsedCount/Id, Id phụ ổn định. UsageLimit null đứng cuối khi tăng dần theo PostgreSQL.")
                .WithOpenApi();
            group.MapPost("/batch", async (WashGo.Application.Contracts.Features.AuditContracts.Commands.AdministrativeBatchCommand command, IPromotionAppService svc) => (await svc.BatchAsync(command)).CustomResult())
                .WithSummary("Xử lý 1–100 mã: action=Delete|Status, isActive đích, items gồm id/expectedVersion; bắt buộc operationId, kết quả từng ID và replay.").WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IPromotionAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết mã giảm giá")
                .WithSummary("Xem chi tiết mã giảm giá.")
                .WithOpenApi();

            group.MapPost("/", async (CreatePromotionCommand command, IPromotionAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới mã giảm giá")
                .WithSummary("Admin tạo mã giảm giá toàn hệ thống (theo % hoặc số tiền cố định).")
                .WithOpenApi();

            group.MapPut("/", async (UpdatePromotionCommand command, IPromotionAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật mã giảm giá")
                .WithSummary("Cập nhật mã giảm giá; không đổi mã khi đã có lượt dùng, tổng lượt không nhỏ hơn số đã dùng.")
                .WithOpenApi();

            group.MapDelete("/{id:guid}", async (Guid id, string? expectedVersion, Guid? operationId, IPromotionAppService svc) =>
                (await svc.DeleteAsync(id, expectedVersion, operationId)).CustomResult())
                .WithName("Xóa mã giảm giá")
                .WithSummary("Xóa mã giảm giá chưa có lượt sử dụng.")
                .WithOpenApi();
            group.MapPut("/{id:guid}/status", async (Guid id, SetPromotionStatusCommand command, IPromotionAppService svc) => (await svc.SetStatusAsync(id, command)).CustomResult())
                .WithSummary("Gán trạng thái isActive đích; bắt buộc expectedVersion, hỗ trợ operationId chống gửi trùng.").WithOpenApi();

            group.MapPatch("/{id:guid}/toggle-active", async (Guid id, IPromotionAppService svc) =>
                (await svc.ToggleActiveAsync(id)).CustomResult())
                .WithName("Bật / Tắt mã giảm giá")
                .WithSummary("Bật / Tắt mã giảm giá.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/statistics", async (Guid id, long? fromDate, long? toDate, IPromotionAppService svc) =>
                (await svc.GetStatisticsAsync(id, fromDate, toDate)).CustomResult())
                .WithName("Thống kê sử dụng mã giảm giá")
                .WithSummary("Số lượt dùng, tổng tiền giảm, doanh thu từ các đơn dùng mã và số liệu theo ngày.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/statistics/export", async (Guid id, long? fromDate, long? toDate, IPromotionAppService svc) =>
            {
                var result = await svc.ExportStatisticsCsvAsync(id, fromDate, toDate);
                if (!result.Succeeded || result.Data == null)
                    return result.CustomResult();
                // UTF-8 BOM so Excel opens the file with the right encoding.
                var bytes = Encoding.UTF8.GetPreamble().AsSpan().ToArray();
                bytes = [.. bytes, .. Encoding.UTF8.GetBytes(result.Data)];
                return Results.File(bytes, "text/csv", $"promotion-{id:N}-statistics.csv");
            })
                .WithName("Xuất báo cáo sử dụng mã giảm giá")
                .WithSummary("Xuất thống kê sử dụng mã giảm giá theo ngày ra file CSV.")
                .WithOpenApi();
        }
    }
}
