using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Domain.Shared.Enums;

namespace WashGo.HttpApi.Host
{
    /// <summary>
    /// A signed-in user stopped by an authorization policy (403, before any endpoint runs) is recorded as Denied with the policy
    /// that refused; the default handler then writes the usual 403 response.
    /// </summary>
    public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden && context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty;
                    var module = context.Request.Path.Value?.Trim('/').Split('/')[0] ?? "api";
                    var units = context.RequestServices.GetRequiredService<IUnitOfWorkManager>();
                    using var uow = units.Begin(requiresNew: true, isTransactional: false);
                    await context.RequestServices.GetRequiredService<AuditLogWriter>().LogAsync(
                        new AuditEntry(module, $"{context.Request.Method} {pattern}", "Request",
                            context.Request.RouteValues.TryGetValue("id", out var id) ? id?.ToString() : null, null)
                        {
                            Result = AuditResult.Denied, ErrorCode = "Forbidden",
                            Reason = "Tài khoản không có quyền thực hiện thao tác này."
                        });
                    await uow.CompleteAsync();
                }
                catch (Exception ex)
                {
                    context.RequestServices.GetRequiredService<ILogger<AuditingAuthorizationResultHandler>>().LogWarning(ex, "Could not audit a forbidden request.");
                }
            }
            await _default.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
