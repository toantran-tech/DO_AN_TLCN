using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.MerchantReport
{
    public class MerchantReportHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/merchant-reports")
                .WithTags("Merchant Report - Báo cáo kinh doanh của tiệm")
                .RequireAuthorization("MerchantOnly");

            group.MapGet("/overview", async ([AsParameters] MerchantReportQuery query, IMerchantReportAppService svc) =>
                (await svc.GetOverviewAsync(query)).CustomResult())
                .WithName("Báo cáo tổng quan của Merchant")
                .WithSummary("KPI kỳ này/kỳ trước (tiền giặt, hoàn tiền, doanh thu thuần, hoa hồng theo tỷ lệ chốt từng đơn, thực nhận, phí đã thu, đơn tạo/hoàn thành/hủy, tỷ lệ đúng hạn có tử/mẫu số), chuỗi theo hour/day/week/month, dịch vụ bán chạy; giờ Việt Nam; một snapshot. period=today|week|month|custom (from,to yyyy-MM-dd). Lỗi có meta.code: InvalidPeriod, InvalidRange, InvalidGranularity, TooManyBuckets.")
                .WithOpenApi();

            group.MapGet("/export", async ([AsParameters] MerchantReportQuery query, IMerchantReportAppService svc) =>
            {
                var result = await svc.ExportCsvAsync(query);
                return result.Succeeded && result.Data != null
                    ? Results.File(result.Data.Content, "text/csv; charset=utf-8", result.Data.FileName)
                    : result.CustomResult();
            })
                .WithName("Xuất báo cáo Merchant (CSV)")
                .WithSummary("Xuất CSV UTF-8 (BOM) cùng bộ lọc/múi giờ/snapshot với báo cáo tổng quan: metadata kỳ, KPI, chuỗi, dịch vụ bán chạy, định nghĩa; mục không có dữ liệu ghi unavailable; chống chèn công thức.")
                .WithOpenApi();
        }
    }
}
