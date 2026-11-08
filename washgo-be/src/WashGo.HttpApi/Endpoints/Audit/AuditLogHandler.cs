using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Audit
{
    public class AuditLogHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/admin")
                .WithTags("Admin - Nhật ký hoạt động")
                .RequireAuthorization("AdminOnly");

            group.MapPost("/audit-logs/filter", async (AuditLogFilterQuery query, IAuditLogAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .WithName("Lọc nhật ký hoạt động")
                .WithSummary("Admin xem nhật ký: ai, lúc nào (UTC), module/thao tác, đối tượng, kết quả (1 thành công, 2 thất bại, 3 bị từ chối), lý do, các trường đã đổi. Lọc theo thời gian (fromDate/toDate Unix giây), người thao tác, vai trò, module, thao tác, loại/mã đối tượng, kết quả, keyword. Chỉ đọc, không sửa/xóa.")
                .WithOpenApi();

            group.MapGet("/audit-logs/{id:guid}", async (Guid id, IAuditLogAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Chi tiết nhật ký hoạt động")
                .WithSummary("Một bản ghi nhật ký kèm chi tiết đã lọc dữ liệu nhạy cảm và correlationId.")
                .WithOpenApi();

            group.MapGet("/me/activities", async ([AsParameters] MyActivityQuery query, IAuditLogAppService svc) =>
                (await svc.GetMyActivitiesAsync(query)).CustomResult())
                .WithName("Hoạt động gần đây của tôi")
                .WithSummary("Các hoạt động của chính Admin đang đăng nhập (đăng nhập, phê duyệt, phân công…), mới nhất trước; page, take ≤ 100, module.")
                .WithOpenApi();
        }
    }
}
