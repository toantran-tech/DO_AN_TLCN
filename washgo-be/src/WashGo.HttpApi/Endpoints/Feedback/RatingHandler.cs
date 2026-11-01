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
                .WithSummary("Customer chấm 1-5 sao, tiêu chí chi tiết, nhận xét và ảnh cho đơn đã hoàn thành; mỗi đơn một lần.")
                .WithOpenApi();

            group.MapGet("/order/{orderId:guid}", async (Guid orderId, IRatingAppService svc) =>
                (await svc.GetByOrderAsync(orderId)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem đánh giá của đơn hàng")
                .WithSummary("Customer xem đánh giá đã gửi cho đơn của mình (null nếu chưa đánh giá).")
                .WithOpenApi();

            group.MapPost("/filter", async (RatingFilterQuery query, IRatingAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Lọc danh sách đánh giá")
                .WithSummary("Admin lọc đánh giá theo số sao, Merchant, thời gian, trạng thái ẩn; Merchant chỉ thấy đánh giá hiển thị của tiệm mình.")
                .WithOpenApi();

            group.MapPatch("/{id:guid}/visibility", async (Guid id, SetRatingVisibilityCommand command, IRatingAppService svc) =>
                (await svc.SetVisibilityAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Ẩn / hiện đánh giá")
                .WithSummary("Admin ẩn đánh giá không phù hợp (bắt buộc lý do) hoặc hiển thị lại; cập nhật điểm trung bình Merchant.")
                .WithOpenApi();
        }
    }
}
