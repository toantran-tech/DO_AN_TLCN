using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Feedback
{
    public class RatingHandler : IEndpointBase
    {
        private const string _endpoint = "/rating";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Rating - Đánh giá dịch vụ");

            group.MapPost("/", async (CreateRatingCommand command, IRatingAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Khách hàng đánh giá đơn hàng")
                .WithSummary("Customer đánh giá dịch vụ WashGo cho đơn đã hoàn thành của mình (1-5 sao tổng thể, tiêu chí tùy chọn, nhận xét, ảnh/video tự upload); không chọn hay thấy Merchant/Shipper. Mỗi đơn một đánh giá (đã có → 409 AlreadyRated). operationId: gửi lại cùng mã trả kết quả cũ, khác nội dung → 409.")
                .WithOpenApi();

            group.MapGet("/order/{orderId:guid}", async (Guid orderId, IRatingAppService svc) =>
                (await svc.GetByOrderAsync(orderId)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem đánh giá của đơn hàng")
                .WithSummary("Customer xem đánh giá đã gửi cho đơn của mình (null nếu chưa); DTO không có thông tin Merchant/Shipper hay kiểm duyệt nội bộ.")
                .WithOpenApi();

            group.MapPost("/filter", async (RatingFilterQuery query, IRatingAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Lọc danh sách đánh giá")
                .WithSummary("Admin lọc đánh giá: sao 1-5, Merchant, trạng thái ẩn, orderId, customerId, ngày tạo, keyword (mã đơn/nhận xét/tên khách); kèm version/canHide/canShow và thông tin kiểm duyệt. Mới nhất trước rồi Id; take ≤ 100. Merchant dùng POST /rating/partner/filter.")
                .WithOpenApi();

            group.MapPost("/partner/filter", async (RatingFilterQuery query, IRatingAppService svc) =>
                (await svc.GetPartnerListAsync(query)).CustomResult())
                .RequireAuthorization("MerchantOnly")
                .WithName("Merchant xem đánh giá dịch vụ")
                .WithSummary("Merchant xem đánh giá đang hiển thị của chính tiệm (DTO riêng, không có mã/id đơn, định danh khách hay dữ liệu kiểm duyệt). Lọc sao 1-5, ngày tạo, keyword trong nhận xét; nhận xét/ảnh/video chỉ có sau khi Admin kiểm duyệt (contentReleased). Mới nhất trước rồi Id; take ≤ 100.")
                .WithOpenApi();

            group.MapGet("/operations/{operationId:guid}", async (Guid operationId, IRatingAppService svc) =>
                (await svc.GetCreateOperationAsync(operationId)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tra cứu kết quả gửi đánh giá")
                .WithSummary("Khách tra cứu kết quả của lần gửi đánh giá theo operationId sau khi mất phản hồi. 404 = chưa thấy kết quả (kiểm tra lại bằng GET /rating/order/{orderId}).")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IRatingAppService svc) => (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Chi tiết đánh giá (Admin)")
                .WithSummary("Một đánh giá với định danh nội bộ (đơn, khách, Merchant), nội dung/media, version, canHide/canShow, thông tin kiểm duyệt. 404 khi không có.")
                .WithOpenApi();

            group.MapPost("/summary", async (RatingFilterQuery query, IRatingAppService svc) => (await svc.GetSummaryAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Thống kê đánh giá (Admin)")
                .WithSummary("Cùng bộ lọc với /rating/filter, một snapshot: tổng, đang hiển thị, đã ẩn, phân bố 1-5 sao (sao tổng thể dịch vụ), điểm trung bình và số lượt của đánh giá đang hiển thị.")
                .WithOpenApi();

            group.MapPost("/batch", async (RatingBatchCommand command, IRatingAppService svc) => (await svc.BatchAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Ẩn/hiện đánh giá hàng loạt")
                .WithSummary("action Hide|Show cho 1-50 đánh giá được chọn (mỗi dòng id + expectedVersion), Hide cần lý do chung 1-500 ký tự. Từng dòng xử lý độc lập: outcome Changed/Unchanged/Skipped/Failed + reasonCode, totals. Cùng operationId trả kết quả cũ, khác nội dung → 409; tra cứu GET /admin-operations/Rating/{operationId}.")
                .WithOpenApi();

            group.MapPatch("/{id:guid}/visibility", async (Guid id, SetRatingVisibilityCommand command, IRatingAppService svc) =>
                (await svc.SetVisibilityAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Ẩn / hiện đánh giá")
                .WithSummary("isHidden là trạng thái đích; bắt buộc expectedVersion (lệch → 409), operationId tùy chọn (gửi lại trả kết quả cũ). Ẩn cần lý do 1-500 ký tự; đã đúng trạng thái → meta.outcome = Unchanged. Ghi nhật ký (giữ lý do ẩn cũ khi hiển thị lại) và cập nhật điểm Merchant trong cùng giao dịch.")
                .WithOpenApi();
        }
    }
}
