using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.OpenApi;
using WashGo.Application.Contracts.Features.StorageContracts.Commands;
using WashGo.Application.Contracts.Features.StorageContracts.Queries;
using WashGo.Application.Contracts.Features.StorageContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Storage;
public class StorageHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/storage").WithTags("Storage - Lưu tại tiệm và giao lại locker").RequireAuthorization();
        group.MapPost("/filter", async (StorageFilterQuery query, IStorageAppService svc) => (await svc.FilterAsync(query)).CustomResult())
            .RequireAuthorization("MerchantOrAdmin").WithSummary("Đơn quá hạn đủ điều kiện chuyển kho/đang chuyển/đang lưu tại tiệm/đang giao lại; keyword mã đơn, state, ngày tạo Unix, page từ 1/take 1–100. Merchant chỉ thấy đơn của tiệm.").WithOpenApi();
        group.MapGet("/summary", async (IStorageAppService svc) => (await svc.SummaryAsync()).CustomResult())
            .RequireAuthorization("MerchantOrAdmin").WithSummary("Số đơn theo trạng thái lưu/giao kho trong phạm vi quyền.").WithOpenApi();
        group.MapGet("/{orderId:guid}/cycles", async (Guid orderId, int? page, int? take, IStorageAppService svc) => (await svc.GetAsync(orderId, page ?? 1, take ?? 20)).CustomResult())
            .WithSummary("Lịch sử vòng lưu kho của đơn được phép xem, page/take; Customer không nhận địa chỉ tiệm/biên nhận/nhân sự.").WithOpenApi();
        group.MapGet("/{orderId:guid}/shippers", async (Guid orderId, IStorageAppService svc) => (await svc.GetShippersAsync(orderId)).CustomResult())
            .RequireAuthorization("MerchantOrAdmin").WithSummary("Shipper active/Available của tiệm, không còn chuyến hiệu lực; kiểm tra lại dưới khóa khi phân công.").WithOpenApi();
        group.MapGet("/{orderId:guid}/quote", async (Guid orderId, Guid lockerId, IStorageAppService svc) => (await svc.QuoteAsync(orderId, lockerId)).CustomResult())
            .RequireAuthorization("CustomerOnly").WithSummary("Báo phí hiện tại và quoteToken ký server, hạn 5 phút, gắn chủ đơn/trạm/vòng. Chưa giữ ô hoặc thu tiền; phí tiếp tục tới lúc Shipper xuất kho.").WithOpenApi();
        group.MapPost("/{orderId:guid}/assign", async (Guid orderId, AssignStorageShipperCommand command, IStorageAppService svc) => (await svc.AssignAsync(orderId, command)).CustomResult())
            .RequireAuthorization("MerchantOrAdmin").WithSummary("Phân công lấy đồ quá hạn về tiệm hoặc giao lại theo yêu cầu khách. Cần operationId/expectedRevision; Admin cần reason. Đổi mã nhiệm vụ, giữ SLA cũ; chưa giải phóng ô.").WithOpenApi();
        group.MapPost("/{orderId:guid}/return-request", async (Guid orderId, RequestStorageReturnCommand command, IStorageAppService svc) => (await svc.RequestReturnAsync(orderId, command)).CustomResult())
            .RequireAuthorization("CustomerOnly").WithSummary("Yêu cầu giao lại/xác nhận lại báo phí sau khi phí tăng. Cần quoteToken, operationId/expectedRevision. Một yêu cầu hiệu lực; cập nhật báo phí không reset SLA 24 giờ. Chưa giữ ô, không tự thanh toán.").WithOpenApi();
        group.MapPost("/{orderId:guid}/verify-legacy", async (Guid orderId, VerifyLegacyStorageCommand command, IStorageAppService svc) => (await svc.ExecuteAsync(orderId, "verify-legacy", command)).CustomResult())
            .RequireAuthorization("AdminOnly").WithSummary("Admin đối chiếu đơn legacy thực sự ở tiệm hiện tại: confirmedAtCurrentShop=true, verificationReference, reason và guard. Không đổi tiền/mốc vào kho, không tạo bàn giao Shipper giả; ghi chứng cứ vào audit trước khi mở giao lại.").WithOpenApi();
        foreach (var action in new[] { "cancel-assignment", "cancel-return", "exception" })
        {
            var step = action;
            group.MapPost("/{orderId:guid}/" + step, async (Guid orderId, StorageCommand command, IStorageAppService svc) => (await svc.ExecuteAsync(orderId, step, command)).CustomResult())
                .RequireAuthorization(step == "cancel-return" ? "CustomerOnly" : "MerchantOrAdmin")
                .WithSummary($"{step}: cần operationId/expectedRevision; hủy chỉ trước khi lấy/xuất kho, không tự hoàn phí đã trả. Exception ghi nhận/audit và báo Admin, không tự đổi người giữ hàng.").WithOpenApi();
        }
        foreach (var action in new[] { "collect", "handover", "depart", "place" })
        {
            var step = action;
            group.MapPost("/{orderId:guid}/" + step, async (Guid orderId, StorageHandoverCommand command, IStorageAppService svc) => (await svc.ConfirmAsync(orderId, step, command)).CustomResult())
                .RequireAuthorization("ShipperOnly")
                .WithSummary($"{step}: Shipper đúng nhiệm vụ gửi taskId, PIN hoặc QR, operationId/expectedRevision. Handover cần receiptReference. Collect mới giải phóng ô; handover bắt đầu phí kho; depart chốt/dừng phí; place chiếm ô phù hợp và cấp mã/hạn nhận mới. Trễ SLA vẫn được giao, ghi lịch sử.").WithOpenApi();
        }
        group.MapGet("/{orderId:guid}/operations/{operationId:guid}", async (Guid orderId, Guid operationId, IStorageAppService svc) => (await svc.GetOperationAsync(orderId, operationId)).CustomResult())
            .WithSummary("Kết quả thao tác kho của chính actor, đúng phạm vi đơn; 404 chưa chứng minh request trước đã thất bại. Retry cùng operationId/nội dung.").WithOpenApi();
    }
}
