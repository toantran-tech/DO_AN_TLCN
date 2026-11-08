using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Audit;

public class AdministrativeOperationHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet("/admin-operations/{module}/{id:guid}", async (string module, Guid id, IAdministrativeOperationAppService service) =>
        (await service.GetAsync(module, id)).CustomResult()).RequireAuthorization("AdminOnly")
        .WithTags("Admin Operations").WithSummary("Tra thao tác của chính Admin: module=Finance|SystemSetting|Locker|Service|Promotion|Incident; batch trả State trong result.data (Processing hoặc Completed).").WithOpenApi();
}
