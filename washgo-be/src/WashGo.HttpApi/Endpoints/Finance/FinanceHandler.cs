using System;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.FinanceContracts.Commands;
using WashGo.Application.Contracts.Features.FinanceContracts.Queries;
using WashGo.Application.Contracts.Features.FinanceContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Finance
{
    public class FinanceHandler : IEndpointBase
    {
        private const string _endpoint = "/finance";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Finance - Quản lý tài chính");

            group.MapPost("/revenue-report", async (FinancePeriodQuery query, IFinanceAppService svc) =>
                (await svc.GetRevenueReportAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Báo cáo doanh thu")
                .WithSummary("Admin xem tổng doanh thu, theo Merchant, theo dịch vụ và theo ngày; lọc khoảng thời gian (Unix giây, mặc định 30 ngày) và Merchant.")
                .WithOpenApi();

            group.MapPost("/revenue-report/export", async (FinancePeriodQuery query, IFinanceAppService svc) =>
            {
                var result = await svc.ExportRevenueReportCsvAsync(query);
                if (!result.Succeeded || result.Data == null)
                    return result.CustomResult();
                // UTF-8 BOM so Excel opens the file with the right encoding.
                byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(result.Data)];
                return Results.File(bytes, "text/csv", $"bao-cao-doanh-thu-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
            })
                .RequireAuthorization("AdminOnly")
                .WithName("Xuất báo cáo doanh thu")
                .WithSummary("Xuất báo cáo doanh thu ra file CSV.")
                .WithOpenApi();

            group.MapPost("/commissions", async (FinancePeriodQuery query, IFinanceAppService svc) =>
                (await svc.GetCommissionsAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xem phí hoa hồng theo Merchant")
                .WithSummary("Theo kỳ: tổng doanh thu tiền giặt, đã hoàn, tỷ lệ hoa hồng, phí hoa hồng thu được và số tiền thực trả Merchant.")
                .WithOpenApi();

            group.MapPost("/settlements", async (CreateSettlementCommand command, IFinanceAppService svc) =>
                (await svc.CreateSettlementAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Tạo kỳ đối soát")
                .WithSummary("Admin chốt số liệu hoa hồng của một Merchant cho một kỳ đã kết thúc; không cho tạo kỳ trùng.")
                .WithOpenApi();
            group.MapPost("/settlements/quote", async (CreateSettlementCommand command, IFinanceAppService svc) => (await svc.QuoteSettlementAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Xem trước thu/hoàn/hoa hồng/thực nhận và các đơn; lấy quoteToken trước khi tạo kỳ. Kỳ đóng, biên UTC gồm cả hai đầu.").WithOpenApi();
            group.MapGet("/operations/{id:guid}", async (Guid id, IFinanceAppService svc) => (await svc.GetOperationAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Tra kết quả ghi Finance theo operationId của chính Admin.").WithOpenApi();

            group.MapPost("/settlements/filter", async (SettlementFilterQuery query, IFinanceAppService svc) =>
                (await svc.GetSettlementsAsync(query)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Danh sách kỳ đối soát")
                .WithSummary("Merchant chỉ xem kỳ của mình. FromDate/ToDate lọc kỳ giao nhau; PaidFromDate/PaidToDate lọc ngày chi trả (Unix giây, bao gồm hai đầu). Keyword tên Merchant/chứng từ; sortBy PeriodFrom/PeriodTo/PaidAt/CreatedOn/MerchantName/PayoutAmount/Status/Id.")
                .WithOpenApi();

            group.MapGet("/settlements/{id:guid}/orders", async (Guid id, IFinanceAppService svc) =>
                (await svc.GetSettlementBreakdownAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Chi tiết kỳ đối soát theo đơn")
                .WithSummary("Từng đơn trong kỳ: tiền thu, hoàn, tỷ lệ hoa hồng chốt trên đơn (null = chưa có snapshot, cần đối chiếu), hoa hồng, thực nhận; kèm số đã chốt của kỳ. Merchant chỉ xem kỳ của mình.")
                .WithOpenApi();
            group.MapGet("/settlements/{id:guid}", async (Guid id, IFinanceAppService svc) => (await svc.GetSettlementAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin").WithSummary("Chi tiết kỳ đối soát gồm version, số tiền đã chốt và chứng từ; Merchant chỉ xem của mình.").WithOpenApi();
            group.MapGet("/settlements/{id:guid}/lines", async (Guid id, int? page, int? take, IFinanceAppService svc) => (await svc.GetSettlementLinesAsync(id, page ?? 1, take ?? 20)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin").WithSummary("Phân trang dòng đối soát từ snapshot; kỳ legacy chưa có snapshot được đánh dấu tại /orders.").WithOpenApi();

            group.MapPost("/settlements/{id:guid}/confirm-paid", async (Guid id, ConfirmSettlementPaidCommand command, IFinanceAppService svc) =>
                (await svc.ConfirmSettlementPaidAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xác nhận đã chi trả đối soát")
                .WithSummary("Admin ghi nhận đã chuyển tiền thực nhận cho Merchant kèm mã tham chiếu.")
                .WithOpenApi();
        }
    }
}
