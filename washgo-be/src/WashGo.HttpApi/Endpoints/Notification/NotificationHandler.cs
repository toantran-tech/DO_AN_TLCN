using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Notification
{
    public class NotificationHandler : IEndpointBase
    {
        private const string _endpoint = "/notification";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Notification - Thông báo")
            .RequireAuthorization();

            group.MapGet("/", async (int? page, int? take, bool? unreadOnly, INotificationAppService svc) =>
                (await svc.GetMineAsync(page ?? 1, take ?? 20, unreadOnly ?? false)).CustomResult())
                .WithName("Xem thông báo của tôi")
                .WithSummary("Danh sách thông báo của tài khoản đang đăng nhập, mới nhất trước; real-time qua SignalR /hubs/notifications (sự kiện 'notification').")
                .WithOpenApi();

            group.MapGet("/unread-count", async (INotificationAppService svc) =>
                (await svc.GetUnreadCountAsync()).CustomResult())
                .WithName("Đếm thông báo chưa đọc")
                .WithSummary("Số thông báo chưa đọc để hiển thị trên biểu tượng chuông.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/read", async (Guid id, INotificationAppService svc) =>
                (await svc.MarkReadAsync(id)).CustomResult())
                .WithName("Đánh dấu thông báo đã đọc")
                .WithSummary("Đánh dấu một thông báo đã đọc; thông báo đã đọc giữ nguyên thời điểm đọc.")
                .WithOpenApi();

            group.MapPost("/read-all", async (INotificationAppService svc) =>
                (await svc.MarkAllReadAsync()).CustomResult())
                .WithName("Đánh dấu tất cả đã đọc")
                .WithSummary("Đánh dấu tất cả thông báo của tôi là đã đọc; trả về số thông báo được cập nhật.")
                .WithOpenApi();

            group.MapDelete("/{id:guid}", async (Guid id, INotificationAppService svc) =>
                (await svc.DeleteAsync(id)).CustomResult())
                .WithName("Xóa thông báo")
                .WithSummary("Xóa một thông báo của tôi.")
                .WithOpenApi();

            group.MapDelete("/", async (INotificationAppService svc) =>
                (await svc.DeleteAllAsync()).CustomResult())
                .WithName("Xóa tất cả thông báo")
                .WithSummary("Xóa tất cả thông báo của tôi; trả về số thông báo đã xóa.")
                .WithOpenApi();
        }
    }
}
