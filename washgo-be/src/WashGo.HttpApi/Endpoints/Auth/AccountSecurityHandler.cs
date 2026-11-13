using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Services;
using WashGo.Core.Endpoint;
using WashGo.Application.Contracts.Features.AuthContracts.Queries;

namespace WashGo.HttpApi.Endpoints.Auth
{
    public class AccountSecurityHandler : IEndpointBase
    {
        private static bool TryIds(HttpContext http, out Guid userId, out Guid sessionId)
        {
            sessionId = Guid.Empty;
            return Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
                   Guid.TryParse(http.User.FindFirstValue("session_id"), out sessionId);
        }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/auth")
                .WithTags("Auth - Bảo mật tài khoản")
                .RequireAuthorization();

            group.MapGet("/security", async (HttpContext http, IAccountSecurityAppService svc) =>
                    TryIds(http, out var u, out var s) ? (await svc.GetStatusAsync(u, s)).CustomResult() : Results.Unauthorized())
                .WithName("Trạng thái bảo mật tài khoản")
                .WithSummary("Có mật khẩu chưa, lần đổi mật khẩu gần nhất, lần đăng nhập gần nhất, đã liên kết Google, số phiên đang hoạt động. 2FA chưa hỗ trợ (twoFactorSupported = false).")
                .WithOpenApi();

            group.MapPost("/change-password", async (ChangePasswordCommand command, HttpContext http, IAccountSecurityAppService svc) =>
                    TryIds(http, out var u, out var s) ? (await svc.ChangePasswordAsync(u, s, command)).CustomResult() : Results.Unauthorized())
                // Per-account limit (wrong current password) instead of the per-IP auth limiter: the caller is signed in.
                .WithName("Đổi mật khẩu")
                .WithSummary("Đổi mật khẩu khi đang đăng nhập: cần mật khẩu hiện tại; mật khẩu mới 8-128 ký tự có hoa/thường/số, khác mật khẩu cũ. Sai mật khẩu hiện tại 5 lần trong 15 phút → 429. revokeOtherSessions (mặc định true) đăng xuất mọi phiên khác, giữ phiên hiện tại.")
                .WithOpenApi();

            group.MapGet("/sessions", async ([AsParameters] SessionListQuery query, HttpContext http, IAccountSecurityAppService svc) =>
                    TryIds(http, out var u, out var s) ? (await svc.GetSessionsAsync(u, s, query)).CustomResult() : Results.Unauthorized())
                .WithName("Danh sách phiên đăng nhập")
                .WithSummary("Các phiên còn hiệu lực của chính tài khoản, phân trang (page, take ≤ 100, total, hasMore), mới hoạt động nhất trước; `current` luôn trả phiên đang dùng. Id là phiên logic ổn định qua các lần làm mới token.")
                .WithOpenApi();

            group.MapPost("/sessions/revoke-others", async (HttpContext http, IAccountSecurityAppService svc) =>
                    TryIds(http, out var u, out var s) ? (await svc.RevokeOtherSessionsAsync(u, s)).CustomResult() : Results.Unauthorized())
                .WithName("Đăng xuất các phiên khác")
                .WithSummary("Đăng xuất mọi phiên khác của tài khoản, GIỮ phiên hiện tại. Access token của các phiên bị thu hồi hết hiệu lực ngay ở request kế tiếp.")
                .WithOpenApi();

            group.MapDelete("/sessions/{id:guid}", async (Guid id, HttpContext http, IAccountSecurityAppService svc) =>
                    TryIds(http, out var u, out var s) ? (await svc.RevokeSessionAsync(u, s, id)).CustomResult() : Results.Unauthorized())
                .WithName("Đăng xuất một phiên")
                .WithSummary("Thu hồi một phiên logic của chính tài khoản (mọi token của phiên, kể cả token mới sau làm mới); chọn phiên hiện tại = đăng xuất. Phiên đã kết thúc → revoked = 0, stale = true. Phiên của tài khoản khác → 404.")
                .WithOpenApi();
        }
    }
}
