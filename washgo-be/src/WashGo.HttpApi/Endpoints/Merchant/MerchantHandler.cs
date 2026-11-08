using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Merchant
{
    public class MerchantHandler : IEndpointBase
    {
        private const string _endpoint = "/merchant";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Merchant - Quản lý đối tác giặt sấy")
            .RequireAuthorization("AdminOnly");

            group.MapGet("/summary", async (IMerchantAppService svc) =>
                (await svc.GetSummaryAsync()).CustomResult())
                .WithName("Admin thống kê Merchant")
                .WithSummary("Tổng Merchant, Active/Inactive/Suspended và yêu cầu cập nhật hồ sơ Pending toàn hệ thống; cùng một snapshot DB, không theo bộ lọc bảng; generatedAt UTC.")
                .WithOpenApi();

            group.MapPost("/filter", async (MerchantFilterQuery request, IMerchantAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách đối tác giặt sấy")
                .WithSummary("Lọc danh sách đối tác giặt sấy.")
                .WithOpenApi();

            group.MapGet("/operations/{operationId:guid}", async (Guid operationId, IMerchantAppService svc) =>
                (await svc.GetOperationAsync(operationId)).CustomResult())
                .WithName("Admin tra cứu thao tác Merchant")
                .WithSummary("Chỉ thao tác đã commit do chính Admin thực hiện: sửa hồ sơ/trạng thái/duyệt/từ chối. Trả snapshot kết quả gốc. 404 không chứng minh request đang chạy đã thất bại; có thể gửi lại cùng operationId và payload.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IMerchantAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết đối tác giặt sấy")
                .WithSummary("Xem chi tiết đối tác giặt sấy.")
                .WithOpenApi();

            group.MapPost("/", async (CreateMerchantCommand command, IMerchantAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới hồ sơ đối tác giặt sấy")
                .WithSummary("Thêm mới hồ sơ đối tác giặt sấy.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateMerchantCommand command, IMerchantAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật hồ sơ đối tác giặt sấy")
                .WithSummary("Sửa hồ sơ: expectedVersion bắt buộc từ GET/list; lệch → 409 kèm hồ sơ mới. operationId tùy chọn chống gửi lặp; cùng key khác payload → 409. Không đổi status tại đây; serviceAreaIds=null giữ vùng, ID giữ lại không mất priority/lịch sử.")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateMerchantStatusCommand command, IMerchantAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái đối tác giặt sấy")
                .WithSummary("Khóa/mở Merchant: expectedVersion bắt buộc; operationId tùy chọn chống gửi lặp. Dùng cùng khóa sức chứa với phân công đơn; khóa cần lý do và confirm khi có đơn chưa xử lý. Replay không gửi notification/audit lại.")
                .WithOpenApi();

            var merchantGroup = app.MapGroup(_endpoint)
                .WithTags("Merchant Dashboard - Bảng điều khiển tiệm giặt")
                .RequireAuthorization("MerchantOnly");

            merchantGroup.MapGet("/dashboard", async ([AsParameters] MerchantDashboardQuery query, IMerchantAppService svc) =>
                (await svc.GetDashboardAsync(query)).CustomResult())
                .WithName("Merchant xem Dashboard tổng quan")
                .WithSummary("Snapshot thống kê 14 trạng thái đơn hàng, 5 đơn mới nhất, các kỳ đối soát gần nhất; hỗ trợ lọc theo Hôm nay / Tuần này / Tháng này / Tùy chọn khoảng ngày.")
                .WithOpenApi();

            merchantGroup.MapGet("/revenue-report", async ([AsParameters] MerchantRevenueReportQuery query, IMerchantAppService svc) =>
                (await svc.GetRevenueReportAsync(query)).CustomResult())
                .WithName("Merchant xem Báo cáo doanh thu & tăng trưởng")
                .WithSummary("Báo cáo doanh thu tiền giặt, hoa hồng sàn, thực nhận, tăng trưởng so với kỳ trước và chuỗi doanh thu theo từng ngày.")
                .WithOpenApi();

            merchantGroup.MapGet("/sla-alerts", async ([AsParameters] MerchantSlaAlertsQuery query, IMerchantAppService svc) =>
                (await svc.GetSlaAlertsAsync(query)).CustomResult())
                .WithName("Merchant xem cảnh báo quá hạn SLA (Overdue Attention)")
                .WithSummary("Cảnh báo các đơn quá hạn lấy đồ, quá hạn giặt sấy hoặc quá hạn trả đồ kèm số phút trễ và thao tác xử lý gợi ý. Tổng cảnh báo tính tại DB trên toàn phạm vi; danh sách ưu tiên phân trang (page, take ≤ 100, alertType). Mốc thời gian trả UTC.")
                .WithOpenApi();
        }
    }
}
