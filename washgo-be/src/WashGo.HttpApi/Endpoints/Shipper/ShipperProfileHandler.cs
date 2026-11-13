using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Endpoint;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Services;

namespace WashGo.HttpApi.Endpoints.Shipper
{
    public class ShipperProfileHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/shipper-profile")
                .WithTags("Shipper Profile - Hồ sơ Shipper")
                .RequireAuthorization("ShipperOnly");

            group.MapGet("/", async (IShipperProfileAppService svc) =>
                (await svc.GetMyProfileAsync()).CustomResult())
                .WithName("Shipper xem hồ sơ")
                .WithSummary("Họ tên, email, SĐT, avatar, phương tiện, biển số, trạng thái, tổng đơn đã giao, đánh giá, Merchant phụ trách.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateShipperProfileCommand command, IShipperProfileAppService svc) =>
                (await svc.UpdateMyProfileAsync(command)).CustomResult())
                .WithName("Shipper cập nhật hồ sơ")
                .WithSummary("Sửa họ tên, SĐT, avatar, phương tiện, biển số; email và Merchant phụ trách không đổi.")
                .WithOpenApi();

            group.MapPut("/work-status", async (UpdateShipperWorkStatusCommand command, IShipperProfileAppService svc) =>
                (await svc.UpdateWorkStatusAsync(command)).CustomResult())
                .WithSummary("Shipper tự Available/Offline; cần expectedRevision và operationId. Còn nhiệm vụ hiệu lực không được Offline; không ghi đè Busy hoặc trạng thái khóa nhân sự. Dùng lookup hồ sơ hiện có.").WithOpenApi();

            group.MapGet("/operations/{operationId:guid}", async (Guid operationId, IShipperProfileAppService svc) =>
                (await svc.GetOperationAsync(operationId)).CustomResult())
                .WithSummary("Tra kết quả cập nhật hồ sơ của chính tài khoản theo operationId; không thấy kết quả không khẳng định request trước thất bại.").WithOpenApi();
            group.MapGet("/activities", async ([AsParameters] MyActivityQuery query, IAuditLogAppService svc) =>
                (await svc.GetMyActivitiesAsync(query)).CustomResult())
                .WithSummary("Nhật ký của chính Shipper, không mở quyền xem nhật ký Admin hoặc tài khoản khác.").WithOpenApi();

            var delivery = app.MapGroup("/shipper-delivery")
                .WithTags("Shipper Delivery - Lịch sử giao nhận")
                .RequireAuthorization("ShipperOnly");

            delivery.MapPost("/history", async (DeliveryHistoryQuery query, IShipperDeliveryAppService svc) =>
                (await svc.GetHistoryAsync(query)).CustomResult())
                .WithName("Lịch sử giao nhận của Shipper")
                .WithSummary("Các lượt lấy/trả đồ Shipper đã hoàn thành: mã đơn, locker, ô, loại nhiệm vụ, thời gian, trạng thái đơn; lọc thời gian và loại nhiệm vụ.")
                .WithOpenApi();

            delivery.MapPost("/summary", async (DeliveryHistoryQuery query, IShipperDeliveryAppService svc) =>
                (await svc.GetSummaryAsync(query)).CustomResult())
                .WithSummary("Tổng lượt lấy/trả theo người thực hiện và ngày hoàn tất; độc lập taskType bảng; tối đa 366 ngày.").WithOpenApi();
            delivery.MapGet("/history/{eventId:guid}", async (Guid eventId, IShipperDeliveryAppService svc) =>
                (await svc.GetEventAsync(eventId)).CustomResult())
                .WithSummary("Chi tiết một lượt do chính Shipper thực hiện, vẫn xem được sau khi phân công lại.").WithOpenApi();
            delivery.MapPost("/lockers/filter", async (DeliveryHistoryQuery query, IShipperDeliveryAppService svc) =>
                (await svc.GetLockersAsync(query)).CustomResult())
                .WithSummary("Lookup tối đa 1.000 trạm trong lịch sử của Shipper, giữ bộ lọc ngày/từ khóa.").WithOpenApi();
            delivery.MapPost("/export", async (DeliveryHistoryQuery query, IShipperDeliveryAppService svc) =>
            {
                var result = await svc.ExportAsync(query);
                return result.Succeeded ? Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(result.Data!)).ToArray(),
                    "text/csv; charset=utf-8", "shipper-delivery.csv") : result.CustomResult();
            }).WithSummary("Xuất toàn bộ lịch sử theo bộ lọc, tối đa 366 ngày/25.000 lượt, không xuất mã bí mật.").WithOpenApi();
        }
    }
}
