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

            group.MapPost("/filter", async (MerchantFilterQuery request, IMerchantAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách đối tác giặt sấy")
                .WithSummary("Lọc danh sách đối tác giặt sấy.")
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
                .WithSummary("Cập nhật hồ sơ đối tác giặt sấy.")
                .WithOpenApi();

            group.MapPut("/status", async (UpdateMerchantStatusCommand command, IMerchantAppService svc) =>
                (await svc.UpdateStatusAsync(command)).CustomResult())
                .WithName("Cập nhật trạng thái đối tác giặt sấy")
                .WithSummary("Cập nhật trạng thái đối tác giặt sấy.")
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

            merchantGroup.MapGet("/sla-alerts", async (IMerchantAppService svc) =>
                (await svc.GetSlaAlertsAsync()).CustomResult())
                .WithName("Merchant xem cảnh báo quá hạn SLA (Overdue Attention)")
                .WithSummary("Cảnh báo các đơn quá hạn lấy đồ, quá hạn giặt sấy hoặc quá hạn trả đồ kèm số phút trễ và thao tác xử lý gợi ý.")
                .WithOpenApi();
        }
    }
}
