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
using System.Net;

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

            group.MapGet("/commission-snapshots/review", async ([AsParameters] CommissionReviewQuery query,
                    ICommissionReviewAppService svc) => (await svc.GetReviewAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Đơn cần đối chiếu tỷ lệ hoa hồng")
                .WithSummary("Đơn đã thu tiền giặt nhưng thiếu tỷ lệ chốt, hoặc 0% cũ chưa xác minh (không suy từ tỷ lệ hiện tại). Kèm tỷ lệ hiện tại của Merchant (chỉ tham khảo), số đã thu, đã vào kỳ đối soát chưa, có lịch sử phân công không. page, take ≤ 100, merchantId.")
                .WithOpenApi();

            group.MapPost("/commission-snapshots/{orderId:guid}/reconcile", async (Guid orderId, ReconcileCommissionCommand command,
                    ICommissionReviewAppService svc) => (await svc.ReconcileAsync(orderId, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Đối chiếu tỷ lệ hoa hồng của đơn")
                .WithSummary("Admin xác nhận/sửa tỷ lệ của một đơn kèm căn cứ (10-1000 ký tự); expectedRate hoặc expectedMissing = true (lệch → 409 RateChanged); đơn đã có khoản trong kỳ đối soát → 409 AlreadySettled. Cập nhật tỷ lệ chụp trên các khoản thu/hoàn chưa đối soát, nguồn = AdminReconciled, ghi nhật ký trước/sau. operationId: gửi lại trả kết quả cũ.")
                .WithOpenApi();

            group.MapPost("/commissions/paged", async (FinancePeriodQuery query, IFinanceAppService svc) =>
                (await svc.GetCommissionsPageAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Phí hoa hồng theo Merchant (phân trang)")
                .WithSummary("Như /commissions nhưng phân trang (page, take ≤ 100, sắp doanh thu thuần giảm dần rồi MerchantId); tổng tiền/hoa hồng/coverage là của toàn kỳ, không phụ thuộc trang. Không dùng tỷ lệ hiện tại cho đơn thiếu snapshot.")
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
                .WithSummary("Chốt kỳ theo thứ tự thời gian với quoteToken; gắn settlementId vào nguồn chưa quyết toán, bao gồm khoản ghi nhận muộn. Payout âm → CarryForward (3), bằng 0 → NoPayout (4), dương → Pending (1). Bù số âm kỳ trước đúng một lần; không cho kỳ trùng.")
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
            group.MapGet("/settlements/{id:guid}/lines", async (Guid id, int? page, int? take, IFinanceAppService svc) => { var r = await svc.GetSettlementLinesAsync(id, page ?? 1, take ?? 20); return r.StatusCode == HttpStatusCode.OK ? Results.Ok(r) : r.StatusCode == HttpStatusCode.NotFound ? Results.NotFound(r) : r.StatusCode == HttpStatusCode.Forbidden ? Results.Json(r, statusCode: 403) : Results.BadRequest(r); })
                .RequireAuthorization("MerchantOrAdmin").WithSummary("Phân trang dòng đối soát; mỗi trang kèm isSnapshot/basis/generatedAt: snapshot lúc chốt kỳ (bất biến) hoặc kỳ cũ tính lại từ nguồn hiện tại (chỉ tham khảo, tổng kỳ giữ nguyên).").WithOpenApi();

            group.MapPost("/settlements/{id:guid}/confirm-paid", async (Guid id, ConfirmSettlementPaidCommand command, IFinanceAppService svc) =>
                (await svc.ConfirmSettlementPaidAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xác nhận đã chi trả đối soát")
                .WithSummary("Admin ghi nhận đã chuyển tiền thực nhận cho Merchant kèm mã tham chiếu.")
                .WithOpenApi();
        }
    }
}
