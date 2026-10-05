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
                .WithName("Đăng nhập hệ thống")
                .WithOpenApi();

            group.MapPost("/register", async (RegisterCommand command, IAuthAppService svc) =>
                (await svc.RegisterAsync(command)).CustomResult())
                .WithName("Đăng ký tài khoản mới")
                .WithOpenApi();

            group.MapPost("/refresh-token", async (RefreshTokenCommand command, IAuthAppService svc) =>
                (await svc.RefreshTokenAsync(command)).CustomResult())
                .WithName("Làm mới Access Token")
                .WithOpenApi();

            group.MapPost("/logout", async (LogoutCommand command, IAuthAppService svc) =>
                (await svc.LogoutAsync(command)).CustomResult())
                .WithName("Đăng xuất hệ thống")
                .WithOpenApi();

            group.MapPost("/send-otp", async (SendOtpCommand command, IAuthAppService svc) =>
                (await svc.SendOtpAsync(command)).CustomResult())
                .WithName("Gửi mã xác thực OTP")
                .WithOpenApi();

            group.MapPost("/verify-otp", async (VerifyOtpCommand command, IAuthAppService svc) =>
                (await svc.VerifyOtpAsync(command)).CustomResult())
                .WithName("Kiểm tra mã OTP")
                .WithOpenApi();

            group.MapPost("/reset-password", async (ResetPasswordCommand command, IAuthAppService svc) =>
                (await svc.ResetPasswordAsync(command)).CustomResult())
                .WithName("Đặt lại mật khẩu")
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
            .WithOpenApi();
        }
    }
}
