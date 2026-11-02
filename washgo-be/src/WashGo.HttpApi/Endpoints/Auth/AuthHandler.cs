using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Auth
{
    public class AuthHandler : IEndpointBase
    {
        private const string _endpoint = "/auth";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint).WithTags("Auth - Xác thực và Phân quyền");

            group.MapPost("/login", async (LoginCommand command, IAuthAppService svc) =>
                (await svc.LoginAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Đăng nhập hệ thống")
                .WithSummary("Đăng nhập bằng email và mật khẩu để nhận access token và refresh token.")
                .WithOpenApi();

            group.MapPost("/google", async (GoogleLoginCommand command, IAuthAppService svc) =>
                (await svc.GoogleLoginAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Đăng nhập bằng Google")
                .WithSummary("Đăng nhập hoặc đăng ký tài khoản Customer bằng Google ID token.")
                .WithOpenApi();

            group.MapPost("/google/admin", async (GoogleLoginCommand command, IAuthAppService svc) =>
                (await svc.GoogleAdminLoginAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Đăng nhập quản trị bằng Google")
                .WithSummary("Đăng nhập quản trị bằng tài khoản Google đã được liên kết.")
                .WithOpenApi();

            group.MapPost("/link-google", async (LinkGoogleCommand command, HttpContext httpContext, IAuthAppService svc) =>
            {
                var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                return (await svc.LinkGoogleAsync(userId, command)).CustomResult();
            })
                .RequireAuthorization()
                .RequireRateLimiting("auth-sensitive")
                .WithName("Liên kết tài khoản Google")
                .WithSummary("Liên kết Google với tài khoản đang đăng nhập bằng Google ID token và mật khẩu hiện tại.")
                .WithOpenApi();

            group.MapPost("/register", async (RegisterCommand command, IAuthAppService svc) =>
                (await svc.RegisterAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Đăng ký tài khoản mới")
                .WithSummary("Hoàn tất đăng ký tài khoản Customer sau khi xác thực email bằng OTP.")
                .WithOpenApi();

            group.MapPost("/refresh-token", async (RefreshTokenCommand command, IAuthAppService svc) =>
                (await svc.RefreshTokenAsync(command)).CustomResult())
                .WithName("Làm mới Access Token")
                .WithSummary("Dùng refresh token để lấy bộ token đăng nhập mới.")
                .WithOpenApi();

            group.MapPost("/logout", async (LogoutCommand command, HttpContext httpContext, IAuthAppService svc) =>
            {
                var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdClaim = httpContext.User.FindFirstValue("session_id");
                if (!Guid.TryParse(userIdClaim, out var userId) ||
                    !Guid.TryParse(sessionIdClaim, out var sessionId))
                {
                    return Results.Unauthorized();
                }

                return (await svc.LogoutAsync(userId, sessionId, command)).CustomResult();
            })
                .RequireAuthorization()
                .WithName("Đăng xuất hệ thống")
                .WithSummary("Đăng xuất và thu hồi phiên đăng nhập hiện tại.")
                .WithOpenApi();

            group.MapPost("/send-otp", async (SendOtpCommand command, IAuthAppService svc) =>
                (await svc.SendOtpAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Gửi mã xác thực OTP")
                .WithSummary("Gửi mã OTP qua email cho mục đích đăng ký hoặc quên mật khẩu.")
                .WithOpenApi();

            group.MapPost("/verify-otp", async (VerifyOtpCommand command, IAuthAppService svc) =>
                (await svc.VerifyOtpAsync(command)).CustomResult())
                .WithName("Kiểm tra mã OTP")
                .WithSummary("Xác minh mã OTP theo email và mục đích xác thực.")
                .WithOpenApi();

            group.MapPost("/reset-password", async (ResetPasswordCommand command, IAuthAppService svc) =>
                (await svc.ResetPasswordAsync(command)).CustomResult())
                .RequireRateLimiting("auth-sensitive")
                .WithName("Đặt lại mật khẩu")
                .WithSummary("Đặt mật khẩu mới bằng email và mã OTP quên mật khẩu.")
                .WithOpenApi();

            group.MapGet("/me", async (HttpContext httpContext, IAuthAppService svc) =>
            {
                var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await svc.GetCurrentUserAsync(userId);
                return result.CustomResult();
            })
            .RequireAuthorization()
            .WithName("Lấy thông tin người dùng hiện tại")
            .WithSummary("Lấy thông tin tài khoản tương ứng với access token đang sử dụng.")
            .WithOpenApi();
        }
    }
}
