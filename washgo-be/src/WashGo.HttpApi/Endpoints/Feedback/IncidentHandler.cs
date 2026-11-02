using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Feedback
{
    public class IncidentHandler : IEndpointBase
    {
        private const string _endpoint = "/incident";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Incident - Báo cáo sự cố & khiếu nại");

            group.MapPost("/", async (CreateIncidentCommand command, IIncidentAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng báo cáo sự cố")
                .WithSummary("Customer báo hư hỏng/mất đồ cho đơn đã hoàn thành trong 3 ngày kể từ khi nhận đồ; bắt buộc ít nhất 1 ảnh.")
                .WithOpenApi();

            group.MapGet("/mine", async (int? page, int? take, IIncidentAppService svc) =>
                (await svc.GetMineAsync(page ?? 1, take ?? 20)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem khiếu nại của tôi")
                .WithSummary("Customer xem danh sách khiếu nại đã gửi và trạng thái xử lý.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IIncidentAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Xem chi tiết khiếu nại")
                .WithSummary("Customer xem khiếu nại của mình; Admin xem mọi khiếu nại.")
                .WithOpenApi();

            group.MapPost("/filter", async (IncidentFilterQuery query, IIncidentAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Lọc danh sách khiếu nại")
                .WithSummary("Admin lọc khiếu nại theo trạng thái, mức độ, loại, Merchant và thời gian; khiếu nại chờ xử lý lên đầu.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/resolve", async (Guid id, ResolveIncidentCommand command, IIncidentAppService svc) =>
                (await svc.ResolveAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xử lý khiếu nại")
                .WithSummary("Admin xác nhận xử lý: bên chịu trách nhiệm, mức bồi thường 30/70/100/200% giá trị đồ; server tính tiền và tạo khoản bồi thường chờ chi trả.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/reject", async (Guid id, RejectIncidentCommand command, IIncidentAppService svc) =>
                (await svc.RejectAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Từ chối khiếu nại")
                .WithSummary("Admin từ chối khiếu nại không hợp lệ kèm lý do.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/confirm-compensation", async (Guid id, ConfirmCompensationCommand command, IIncidentAppService svc) =>
                (await svc.ConfirmCompensationAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xác nhận đã chi trả bồi thường")
                .WithSummary("Admin ghi nhận đã chuyển tiền bồi thường thủ công kèm mã tham chiếu.")
                .WithOpenApi();
        }
    }
}
