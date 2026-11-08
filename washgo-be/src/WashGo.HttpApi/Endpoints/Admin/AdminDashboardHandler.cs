using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AdminContracts;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Admin
{
    public class AdminDashboardHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/admin")
                .WithTags("Admin - Dashboard và cảnh báo")
                .RequireAuthorization("AdminOnly");

            group.MapGet("/dashboard", async ([AsParameters] AdminDashboardQuery query, IAdminDashboardAppService svc) =>
                    (await svc.GetDashboardAsync(query)).CustomResult())
                .WithName("Dashboard Admin")
                .WithSummary("Tổng quan một snapshot (generatedAt): Customer (đang hoạt động/khóa/mới trong kỳ), Merchant theo trạng thái, đơn tạo/hoàn thành/hủy trong kỳ và hiện trạng (chờ phân công, quá hạn, đang xử lý, theo trạng thái), doanh thu theo thời điểm giao dịch, Locker/ô trống, top Merchant (công thức trong definitions). period = today|week|month (mặc định)|custom|all; so sánh kỳ trước cùng độ dài, kỳ trước = 0 → changePercent null + isNew. Mốc trả UTC.")
                .WithOpenApi();

            group.MapGet("/alerts", async ([AsParameters] AdminAlertsQuery query, IAdminDashboardAppService svc) =>
                    (await svc.GetAlertsAsync(query)).CustomResult())
                .WithName("Cảnh báo vận hành Admin")
                .WithSummary("Cảnh báo theo quy tắc (source = Rule, không phải dự báo/AI): chờ phân công quá 60 phút, Merchant trễ lấy/trả đồ, quá thời gian giặt dự kiến, khách quá hạn lấy đồ/thanh toán, quá hạn gửi đồ, Locker còn ≤ 10% ô. summary đếm từng loại trên toàn hệ thống; danh sách lọc type/severity, phân trang (page, take ≤ 100), mức cao trước rồi quá hạn lâu nhất. Mốc UTC.")
                .WithOpenApi();

            group.MapGet("/lookups/merchants", async ([AsParameters] AdminLookupQuery query, IAdminDashboardAppService svc) =>
                    (await svc.LookupMerchantsAsync(query)).CustomResult())
                .WithName("Lookup Merchant")
                .WithSummary("Danh sách gọn cho dropdown: id, tên, trạng thái; keyword theo tên, status (MerchantStatus), page, take ≤ 50, sắp theo tên.")
                .WithOpenApi();

            group.MapGet("/lookups/lockers", async ([AsParameters] AdminLookupQuery query, IAdminDashboardAppService svc) =>
                    (await svc.LookupLockersAsync(query)).CustomResult())
                .WithName("Lookup Locker")
                .WithSummary("Danh sách gọn cho dropdown: id, mã, tên, trạng thái, ô trống/tổng ô; keyword theo tên/mã, status (LockerStatus), page, take ≤ 50, sắp theo tên.")
                .WithOpenApi();
        }
    }
}
