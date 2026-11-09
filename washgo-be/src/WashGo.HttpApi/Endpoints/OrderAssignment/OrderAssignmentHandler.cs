using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Commands;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Queries;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.OrderAssignment
{
    public class OrderAssignmentHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/order-assignment")
                .WithTags("Order Assignment - Phân công đơn cho Merchant")
                .RequireAuthorization("AdminOnly");

            group.MapPost("/filter", async (WaitingOrderFilterQuery query, IOrderAssignmentAppService svc) =>
                (await svc.GetWaitingOrdersAsync(query)).CustomResult())
                .WithName("Danh sách đơn chờ phân công")
                .WithSummary("Đơn đã gửi đồ nhưng chưa có Merchant; highlight đơn chờ quá 60 phút; lọc khu vực, vùng phục vụ, thời gian gửi, chỉ đơn quá hạn.")
                .WithOpenApi();

            group.MapGet("/{orderId:guid}/candidates", async (Guid orderId, IOrderAssignmentAppService svc) =>
                (await svc.GetCandidatesAsync(orderId)).CustomResult())
                .WithName("Merchant có thể nhận đơn")
                .WithSummary("Merchant đang hoạt động phụ trách locker của đơn: đánh giá, tải lượng X/Y, khoảng cách, độ ưu tiên, điểm gợi ý.")
                .WithOpenApi();

            group.MapPost("/{orderId:guid}/assign", async (Guid orderId, AssignOrderCommand command, HttpContext http, IOrderAssignmentAppService svc) =>
            {
                if (command.OperationId == null && Guid.TryParse(http.Request.Headers["Idempotency-Key"].ToString(), out var key))
                    command.OperationId = key;
                return (await svc.AssignAsync(orderId, command)).CustomResult();
            })
                .WithName("Gán đơn cho Merchant")
                .WithDescription("Hợp đồng tính đến hết ngày kết thúc theo giờ Việt Nam; hết hạn từ 00:00 ngày kế tiếp trả meta.code=ContractExpired. Null là không thời hạn. Không khóa tài khoản hoặc chặn xử lý đơn đã nhận.")
                .WithSummary("Ghi nhận Merchant cho đơn (phải phụ trách locker, còn sức chứa), ghi lịch sử và báo Merchant. Khóa sức chứa theo Merchant nên nhiều Admin cùng lúc không vượt tải. Tùy chọn: expectedRevision, expectedCommissionRate (lệch → 409 RevisionMismatch/CommissionChanged), operationId/Idempotency-Key (gửi lại trả kết quả cũ, cùng mã khác nội dung → 409). Lỗi có meta.code.")
                .WithOpenApi();

            group.MapGet("/operations/{operationId:guid}", async (Guid operationId, IOrderAssignmentAppService svc) =>
                (await svc.GetOperationAsync(operationId)).CustomResult())
                .WithName("Tra cứu kết quả phân công theo operationId")
                .WithSummary("Sau timeout: các cặp đơn–Merchant đã ghi nhận theo operationId (đơn lẻ hoặc lô); 404 = chưa thực hiện, có thể gửi lại cùng mã.")
                .WithOpenApi();

            group.MapPost("/auto-suggest", async (AutoSuggestCommand command, IOrderAssignmentAppService svc) =>
                (await svc.SuggestAsync(command)).CustomResult())
                .WithName("Gợi ý phân công tự động (AI)")
                .WithSummary("Chấm điểm Merchant theo độ ưu tiên tại locker, tải lượng, đánh giá, khoảng cách; gợi ý Merchant phù hợp nhất cho từng đơn.")
                .WithOpenApi();

            group.MapPost("/auto-assign", async (AutoAssignCommand command, HttpContext http, IOrderAssignmentAppService svc) =>
            {
                if (command.OperationId == null && Guid.TryParse(http.Request.Headers["Idempotency-Key"].ToString(), out var key))
                    command.OperationId = key;
                return (await svc.AutoAssignAsync(command)).CustomResult();
            })
                .WithName("Xác nhận phân công tự động")
                .WithSummary("Admin xác nhận các cặp đơn – Merchant đã gợi ý; ghi nhận tất cả hoặc không đơn nào. Khóa sức chứa các Merchant theo thứ tự cố định; mỗi cặp nhận expectedRevision/expectedCommissionRate; operationId cho cả lô (gửi lại trả kết quả cũ).")
                .WithOpenApi();
        }
    }
}
