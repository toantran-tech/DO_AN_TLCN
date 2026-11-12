using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.WashOrder
{
    public class WashOrderHandler : IEndpointBase
    {
        private const string _endpoint = "/wash-order";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Wash Order - Quản lý đơn hàng");

            group.MapPost("/filter", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Lọc danh sách đơn WashGo")
                .WithSummary("Lọc và phân trang đơn hàng trong phạm vi quyền của Admin, Merchant hoặc Shipper. keyword tìm mã đơn WashGo hoặc mã nội bộ Merchant; fromDate/toDate là Unix giây (ngày tạo đơn, tính cả hai đầu), khoảng ngược trả 400.")
                .Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status200OK).Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status400BadRequest).Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapPost("/summary", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetSummaryAsync(request)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Thống kê đơn theo bộ lọc")
                .WithSummary("Tổng và số đơn từng trạng thái trong một lần đếm, theo cùng keyword/paymentStatus/fromDate/toDate của danh sách (không áp dụng status, trang, sắp xếp). Merchant chỉ thấy đơn của tiệm mình.")
                .Produces<BaseResponse<WashOrderSummaryDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderSummaryDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderSummaryDto>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapPost("/mine", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetMyOrdersAsync(request)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem danh sách đơn hàng của tôi")
                .WithSummary("Customer lọc và phân trang các đơn hàng của mình.")
                .Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status200OK).Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status400BadRequest).Produces<PaginatedResult<WashOrderResultDto>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Xem chi tiết đơn WashGo")
                .WithSummary("Xem chi tiết đơn hàng được phép truy cập theo tài khoản đang đăng nhập.")
                .Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapGet("/{id:guid}/access-codes", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetAccessCodesAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin xem mã mở tủ của đơn")
                .WithSummary("Mã gửi/nhận đồ và mã nhiệm vụ Shipper của một đơn, chỉ Admin (hỗ trợ đặc biệt); danh sách/chi tiết đơn của Admin không còn trả các mã này. Mỗi lần xem được ghi vào lịch sử đơn.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/receipt", async (Guid id, IOrderReceiptAppService svc) =>
                (await svc.GetAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Biên lai thanh toán")
                .WithSummary("Biên lai nội bộ (không phải hóa đơn GTGT): số/ngày biên lai cố định, bên bán (Merchant, MST), bên mua, dịch vụ, phụ phí, giảm giá, phí đã thu, giao dịch. Khách chủ đơn, Merchant của đơn, Admin; đơn phải có thanh toán thành công.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/receipt/pdf", async (Guid id, IOrderReceiptAppService svc) =>
            {
                var result = await svc.GetPdfAsync(id);
                return result.Succeeded && result.Data != null
                    ? Results.File(result.Data.Content, "application/pdf", result.Data.FileName)
                    : result.CustomResult();
            })
                .RequireAuthorization()
                .WithName("Tải biên lai PDF")
                .WithSummary("Tải biên lai thanh toán dạng PDF (A5, tiếng Việt).")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo đơn hàng WashGo mới")
                .WithSummary("Customer tạo đơn hàng và đặt ô tủ gửi đồ; có thể kèm mã giảm giá (promotionCode).")
                .WithOpenApi();

            group.MapPost("/apply-voucher", async (ApplyVoucherCommand command, IWashOrderAppService svc) =>
                (await svc.ApplyVoucherAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Báo giá đơn và áp dụng mã giảm giá")
                .WithSummary("Customer xem giá đơn tính từ server, danh sách mã giảm giá khả dụng và kiểm tra mã đã chọn trước khi tạo đơn.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/cancellation-quote", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetCancellationQuoteAsync(id)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem phí hủy đơn dự kiến")
                .WithSummary("Customer xem điều kiện hủy, phí 0%/30%/50%, tiền hoàn và phí còn thiếu; tính lại khi xác nhận hủy.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/admin-cancellation-quote", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetAdminCancellationQuoteAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin xem dự kiến hủy đơn quá hạn gửi đồ")
                .WithSummary("Chỉ đọc: được hủy hay không (reasonCode), hạn gửi đồ, giờ server, phí hủy theo chính sách, tiền đã thu được xác minh, tiền hoàn, phí còn thiếu, revision.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/cancel-expired-deposit", async (Guid id, AdminCancelExpiredDepositCommand command, HttpContext http, IWashOrderAppService svc) =>
                (await svc.CancelExpiredDepositAsync(id, OrderWriteHeaders.Apply(command, http))).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin hủy đơn quá hạn gửi đồ")
                .WithSummary("Hủy đơn còn chờ gửi đồ đã quá hạn (giờ server): lý do 1–500 ký tự, giải phóng đúng ô đang giữ chỗ, áp chính sách phí hủy hiện hành, ghi lịch sử và báo khách. expectedRevision/expectedStatus/expectedFeeAmount lệch → 409 (QuoteChanged); operationId/Idempotency-Key gửi lại không hủy lại; ô không còn giữ chỗ → 409 BoxNotReserved.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/cancel", async (Guid id, CancelWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CancelAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng hủy đơn của mình")
                .WithSummary("Customer hủy Pending, Deposited hoặc Collected đã cân; tính phí và hoàn tiền, giữ ô đang có đồ đến khi nhận lại.")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashOrderStatusCommand command, HttpContext http, IWashOrderAppService svc) =>
                (await svc.UpdateStatusAsync(OrderWriteHeaders.Apply(command, http))).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Cập nhật trạng thái đơn")
                .WithSummary("Cập nhật trạng thái vận hành đơn theo quyền; không cho chuyển sang đã thanh toán hoặc đã hủy. Tùy chọn expectedRevision (If-Match)/expectedStatus: lệch thì 409 kèm đơn hiện tại; operationId (Idempotency-Key): gửi lại không thực hiện lại.")
                .Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status404NotFound).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status409Conflict)
                .WithOpenApi();

            group.MapPut("/{id:guid}/actual-weight", async (Guid id, UpdateActualWeightCommand command, HttpContext http, IWashOrderAppService svc) =>
                (await svc.UpdateActualWeightAsync(id, OrderWriteHeaders.Apply(command, http))).CustomResult())
                .RequireAuthorization("MerchantOnly")
                .WithName("Merchant cập nhật cân nặng thực tế và chốt giá")
                .WithSummary("Merchant cân đồ đã thu gom, tính lại giá và thời gian dự kiến; đơn giữ trạng thái Collected. Hỗ trợ expectedRevision/expectedStatus (409) và operationId như PUT /status.")
                .Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status404NotFound).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status409Conflict)
                .WithOpenApi();

            group.MapPost("/move-to-storage", async (MoveOrderToStorageCommand command, HttpContext http, IWashOrderAppService svc) =>
                (await svc.MoveToStorageAsync(OrderWriteHeaders.Apply(command, http))).CustomResult())
                .RequireAuthorization("ShipperOrAdmin")
                .WithName("Ghi nhận chuyển đồ quá hạn sang kho")
                .WithSummary("Shipper hoặc Admin ghi nhận chuyển đồ quá hạn khỏi locker sang kho. Hỗ trợ expectedRevision/expectedStatus (409) và operationId (gửi lại không thực hiện lại; tra cứu bằng GET /wash-order/{id}/operations/{operationId}). Điều kiện xem trước ở allowedActions.canMoveToStorage.")
                .Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status404NotFound).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status409Conflict)
                .WithOpenApi();

            group.MapPut("/assign-shipper", async (AssignShipperCommand command, HttpContext http, IWashOrderAppService svc) =>
                (await svc.AssignShipperAsync(OrderWriteHeaders.Apply(command, http))).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Phân công Shipper cho đơn")
                .WithSummary("Merchant hoặc Admin phân công riêng chuyến lấy đồ hoặc chuyến trả đồ theo trạng thái đơn. Hỗ trợ expectedRevision/expectedStatus (409) và operationId như PUT /status.")
                .Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status200OK).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status404NotFound).Produces<BaseResponse<WashOrderDetailDto>>(StatusCodes.Status409Conflict)
                .WithOpenApi();

            group.MapGet("/{id:guid}/operations/{operationId:guid}", async (Guid id, Guid operationId, IWashOrderAppService svc) =>
                (await svc.GetOperationAsync(id, operationId)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Tra cứu kết quả thao tác trên đơn")
                .WithSummary("Sau timeout/lỗi mạng: tra theo operationId đã gửi xem thao tác status/actual-weight/assign-shipper đã thực hiện chưa; 404 = chưa thực hiện, có thể gửi lại cùng operationId.")
                .Produces<BaseResponse<OrderOperationDto>>(StatusCodes.Status200OK).Produces<BaseResponse<OrderOperationDto>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<OrderOperationDto>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapGet("/{id:guid}/available-shippers", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetAvailableShippersAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Xem Shipper khả dụng cho đơn")
                .WithSummary("Liệt kê Shipper đủ điều kiện nhận nhiệm vụ lấy hoặc trả đồ của đơn hàng.")
                .Produces<BaseResponse<List<AvailableShipperDto>>>(StatusCodes.Status200OK).Produces<BaseResponse<List<AvailableShipperDto>>>(StatusCodes.Status400BadRequest).Produces<BaseResponse<List<AvailableShipperDto>>>(StatusCodes.Status404NotFound)
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, WashOrderFilterQuery query, IWashOrderAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Lấy giá trị distinct cho bộ lọc cột đơn hàng")
                .WithSummary("Lấy giá trị distinct cho bộ lọc cột đơn hàng.")
                .WithOpenApi();

            // QR/PIN simulation delivery & pickup endpoints
            group.MapPost("/{id:guid}/deposit", async (Guid id, DepositWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.DepositAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng gửi đồ vào locker")
                .WithSummary("Khách hàng nhập mã PIN hoặc quét QR để mở ô tủ gửi đồ và hoàn tất gửi đồ.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/shipper-collect", async (Guid id, ShipperCollectOrderCommand command, IWashOrderAppService svc) =>
                (await svc.ShipperCollectAsync(id, command)).CustomResult())
                .RequireAuthorization("ShipperOrAdmin")
                .WithName("Shipper lấy đồ từ locker về tiệm")
                .WithSummary("Shipper xác thực mã nhiệm vụ lấy đồ, mở ô tủ gửi và chuyển trạng thái đơn sang Collected.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/shipper-return", async (Guid id, ShipperReturnOrderCommand command, IWashOrderAppService svc) =>
                (await svc.ShipperReturnAsync(id, command)).CustomResult())
                .RequireAuthorization("ShipperOrAdmin")
                .WithName("Shipper trả đồ vào locker")
                .WithSummary("Shipper xác thực mã nhiệm vụ trả đồ, chọn ô tủ trống phù hợp và chuyển đơn sang ReadyToReturn.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/open-pickup-box", async (Guid id, OpenPickupBoxCommand command, IWashOrderAppService svc) =>
                (await svc.OpenPickupBoxAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng mở tủ nhận đồ (Bước 1)")
                .WithSummary("Khách hàng xác thực mã nhận đồ và kiểm tra thanh toán, hệ thống mở tủ và cấp token phiên lấy đồ.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/confirm-pickup", async (Guid id, ConfirmPickupCommand command, IWashOrderAppService svc) =>
                (await svc.ConfirmPickupAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng xác nhận đã lấy đồ và đóng tủ (Bước 2)")
                .WithSummary("Khách hàng xác nhận hoàn tất lấy đồ bằng token phiên; giải phóng ô tủ và hoàn tất đơn hàng.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/pickup-storage", async (Guid id, PickupFromStorageCommand command, IWashOrderAppService svc) =>
                (await svc.PickupFromStorageAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng nhận đồ tại kho")
                .WithSummary("Khách hàng hoàn tất nhận đồ đối với đơn quá hạn đã chuyển sang lưu kho sau khi thanh toán đủ phí.")
                .WithOpenApi();
        }
    }
}
