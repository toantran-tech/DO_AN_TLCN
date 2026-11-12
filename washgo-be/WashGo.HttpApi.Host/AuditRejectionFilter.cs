using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.HttpApi.Host
{
    /// <summary>
    /// Records every rejected write (validation, not found, version/operation conflict, rate limit) of a signed-in user in the
    /// audit trail: Denied for 401/403/409/429, Failed for other 4xx. Successful writes are logged by the services themselves;
    /// replays return 200 and are not logged again. Auth endpoints log their own attempts.
    /// </summary>
    public sealed class AuditRejectionFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var result = await next(context);
            var http = context.HttpContext;
            if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method) ||
                http.User?.Identity?.IsAuthenticated != true || http.Request.Path.StartsWithSegments("/auth"))
                return result;
            var status = (result as IStatusCodeHttpResult)?.StatusCode;
            if (status is not (>= 400 and < 500))
                return result;
            var body = (result as IValueHttpResult)?.Value as BaseResponse;
            string? code = null;
            if (body?.GetType().GetProperty("Meta")?.GetValue(body) is { } meta)
                code = meta.GetType().GetProperty("code")?.GetValue(meta)?.ToString()
                       ?? (meta is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.Object &&
                           json.TryGetProperty("code", out var c) ? c.GetString() : null);
            var pattern = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? string.Empty;
            var module = http.Request.Path.Value?.Trim('/').Split('/').FirstOrDefault() ?? "api";
            var resource = http.Request.RouteValues.TryGetValue("id", out var id) ? id?.ToString() : null;
            try
            {
                var units = http.RequestServices.GetRequiredService<IUnitOfWorkManager>();
                using var uow = units.Begin(requiresNew: true, isTransactional: false);
                await http.RequestServices.GetRequiredService<AuditLogWriter>().LogAsync(
                    new AuditEntry(module, $"{http.Request.Method} {pattern}", "Request", resource, null)
                    {
                        Result = status is 401 or 403 or 409 or 429 ? AuditResult.Denied : AuditResult.Failed,
                        ErrorCode = code ?? $"HTTP{status}",
                        Reason = body?.Message
                    });
                await uow.CompleteAsync();
            }
            catch (Exception ex)
            {
                http.RequestServices.GetRequiredService<ILogger<AuditRejectionFilter>>().LogWarning(ex, "Could not audit a rejected request.");
            }
            return result;
        }
    }
}
