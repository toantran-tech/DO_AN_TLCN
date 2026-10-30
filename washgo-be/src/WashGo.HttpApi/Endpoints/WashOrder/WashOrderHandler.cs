using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
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
                .WithSummary("Lọc và phân trang đơn hàng trong phạm vi quyền của Admin, Merchant hoặc Shipper.")
                .WithOpenApi();

            group.MapPost("/mine", async (WashOrderFilterQuery request, IWashOrderAppService svc) =>
                (await svc.GetMyOrdersAsync(request)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem danh sách đơn hàng của tôi")
                .WithSummary("Customer lọc và phân trang các đơn hàng của mình.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Xem chi tiết đơn WashGo")
                .WithSummary("Xem chi tiết đơn hàng được phép truy cập theo tài khoản đang đăng nhập.")
                .WithOpenApi();

            group.MapPost("/", async (CreateWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo đơn hàng WashGo mới")
                .WithSummary("Customer tạo đơn hàng và đặt ô tủ gửi đồ.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/cancel", async (Guid id, CancelWashOrderCommand command, IWashOrderAppService svc) =>
                (await svc.CancelAsync(id, command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng hủy đơn của mình")
                .WithSummary("Customer hủy đơn của mình khi đơn còn ở trạng thái chờ gửi đồ.")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateWashOrderStatusCommand command, IWashOrderAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .RequireAuthorization("OrderStatusUpdater")
                .WithName("Cập nhật trạng thái đơn")
                .WithSummary("Cập nhật trạng thái vận hành đơn theo quyền; không cho chuyển sang đã thanh toán hoặc đã hủy.")
                .WithOpenApi();

            group.MapPut("/{id:guid}/actual-weight", async (Guid id, UpdateActualWeightCommand command, IWashOrderAppService svc) =>
                (await svc.UpdateActualWeightAsync(id, command)).CustomResult())
                .RequireAuthorization("MerchantOnly")
                .WithName("Merchant cập nhật cân nặng thực tế và chốt giá")
                .WithSummary("Merchant cân đồ đã thu gom, tính lại giá và thời gian dự kiến; đơn giữ trạng thái Collected.")
                .WithOpenApi();

            group.MapPost("/move-to-storage", async (MoveOrderToStorageCommand command, IWashOrderAppService svc) =>
                (await svc.MoveToStorageAsync(command)).CustomResult())
                .RequireAuthorization("ShipperOrAdmin")
                .WithName("Ghi nhận chuyển đồ quá hạn sang kho")
                .WithSummary("Shipper hoặc Admin ghi nhận chuyển đồ quá hạn khỏi locker sang kho.")
                .WithOpenApi();

            group.MapPut("/assign-shipper", async (AssignShipperCommand command, IWashOrderAppService svc) =>
                (await svc.AssignShipperAsync(command)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Phân công Shipper cho đơn")
                .WithSummary("Merchant hoặc Admin phân công riêng chuyến lấy đồ hoặc chuyến trả đồ theo trạng thái đơn.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/available-shippers", async (Guid id, IWashOrderAppService svc) =>
                (await svc.GetAvailableShippersAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Xem Shipper khả dụng cho đơn")
                .WithSummary("Liệt kê Shipper đủ điều kiện nhận nhiệm vụ lấy hoặc trả đồ của đơn hàng.")
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
