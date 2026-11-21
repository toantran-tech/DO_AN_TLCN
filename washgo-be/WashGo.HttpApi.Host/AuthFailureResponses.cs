using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using WashGo.Domain.Entities.Identity;

namespace WashGo.HttpApi.Host
{
    /// <summary>
    /// 401/403 bodies with a stable meta.code so clients can tell a locked/deleted account from an ended or expired
    /// session, a missing token or a missing permission. Account state is reported only for a token whose signature
    /// was valid (the caller proved it was signed in as that account); revocation alone never implies a reason.
    /// </summary>
    public static class AuthFailureResponses
    {
        private const string CodeKey = "washgo.auth-failure.code";
        private const string RoleKey = "washgo.auth-failure.role";

        public const string AccountLocked = "AccountLocked";
        public const string AccountDeleted = "AccountDeleted";
        public const string SessionRevoked = "SessionRevoked";
        public const string SessionExpired = "SessionExpired";
        public const string TokenExpired = "TokenExpired";
        public const string InvalidToken = "InvalidToken";
        public const string Unauthenticated = "Unauthenticated";
        public const string Forbidden = "Forbidden";

        /// <summary>Why a signed, unexpired access token can no longer be used; null when it is still valid.</summary>
        public static string? Classify(Guid userId, User? user, RefreshToken? session, DateTime now)
        {
            if (user == null || session == null || session.UserId != userId) return SessionRevoked;
            // The account's own state is checked first: it is reliable, unlike guessing from a revoked session.
            if (user.IsDeleted) return AccountDeleted;
            if (!user.IsActive) return AccountLocked;
            if (session.IsRevoked) return SessionRevoked;
            return session.ExpiryDate <= now ? SessionExpired : null;
        }

        public static void Remember(HttpContext http, string code, ClaimsPrincipal? principal)
        {
            http.Items[CodeKey] = code;
            if (principal?.FindFirstValue(ClaimTypes.Role) is { } role) http.Items[RoleKey] = role;
        }

        public static async Task WriteChallengeAsync(JwtBearerChallengeContext context)
        {
            var http = context.HttpContext;
            var code = http.Items[CodeKey] as string ?? context.AuthenticateFailure switch
            {
                SecurityTokenExpiredException => TokenExpired,
                null => Unauthenticated,
                _ => InvalidToken
            };
            context.HandleResponse();
            if (http.Response.HasStarted) return;
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            http.Response.Headers.WWWAuthenticate = code == Unauthenticated ? "Bearer" : "Bearer error=\"invalid_token\"";
            await http.Response.WriteAsJsonAsync(new
            {
                succeeded = false, statusCode = 401, message = Message(code, http.Items[RoleKey] as string), meta = new { code }
            });
        }

        public static async Task WriteForbiddenAsync(ForbiddenContext context)
        {
            var http = context.HttpContext;
            if (http.Response.HasStarted) return;
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await http.Response.WriteAsJsonAsync(new
            {
                succeeded = false, statusCode = 403, message = "Tài khoản không có quyền thực hiện thao tác này.", meta = new { code = Forbidden }
            });
        }

        private static string Message(string code, string? role) => code switch
        {
            AccountLocked => role == "Shipper"
                ? "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Merchant."
                : "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin.",
            AccountDeleted => "Tài khoản này đã bị xóa. Vui lòng liên hệ Admin.",
            SessionRevoked or SessionExpired => "Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.",
            TokenExpired => "Phiên truy cập đã hết hạn; hãy làm mới phiên đăng nhập.",
            InvalidToken => "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
            _ => "Vui lòng đăng nhập."
        };
    }
}
