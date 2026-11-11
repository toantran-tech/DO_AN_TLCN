using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Shipper;

public class ShipperTaskHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/shipper-dashboard", async (string? period, long? fromDate, long? toDate, IShipperDashboardAppService svc) =>
            (await svc.GetAsync(new ShipperDashboardQuery { Period = period ?? "day", FromDate = fromDate ?? 0, ToDate = toDate ?? 0 })).CustomResult()).RequireAuthorization("ShipperOnly").WithTags("Shipper Dashboard")
            .WithSummary("Dashboard snapshot theo giờ Việt Nam: pending hiện tại, hoàn tất trong kỳ, KPI có sample/coverage và kỳ trước; period=day/week/month/custom.").WithOpenApi();
        var group = app.MapGroup("/shipper-tasks").WithTags("Shipper Tasks - Nhiệm vụ của tôi").RequireAuthorization("ShipperOnly");
        group.MapPost("/filter", async (MyShipperTaskQuery query, IMyShipperTaskAppService svc) =>
            (await svc.GetListAsync(query)).CustomResult()).WithSummary("Nhiệm vụ của Shipper theo JWT; lọc ngày phân công, mã đơn, loại/trạng thái; sort AssignedAt/Deadline; không chứa PIN/QR.").WithOpenApi();
        group.MapPost("/summary", async (MyShipperTaskQuery query, IMyShipperTaskAppService svc) =>
            (await svc.GetSummaryAsync(query)).CustomResult()).WithSummary("Snapshot nhiệm vụ hiện đang chờ, theo keyword/ngày phân công; độc lập loại/trạng thái bảng.").WithOpenApi();
        group.MapGet("/{taskId:guid}", async (Guid taskId, IMyShipperTaskAppService svc) =>
            (await svc.GetAsync(taskId)).CustomResult()).WithSummary("Chi tiết một lần phân công; assignmentVersion=taskId, canCollect/canReturn và đúng trạm.").WithOpenApi();
        group.MapGet("/{taskId:guid}/operations/{operationId:guid}", async (Guid taskId, Guid operationId, IMyShipperTaskAppService svc) =>
            (await svc.GetOperationAsync(taskId, operationId)).CustomResult())
            .WithSummary("Tra kết quả thao tác của chính Shipper; unknown không có nghĩa request cũ đã thất bại, chỉ retry cùng operationId.").WithOpenApi();
        group.MapGet("/{taskId:guid}/credentials", async (Guid taskId, HttpContext http, IMyShipperTaskAppService svc) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.Pragma = "no-cache";
            return (await svc.GetCredentialsAsync(taskId)).CustomResult();
        }).WithSummary("Xem mã nhiệm vụ đang hoạt động của chính Shipper; có audit, không cache. Không phải thao tác mở tủ.").WithOpenApi();
    }
}
