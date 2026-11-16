using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.ServiceCategory;
public class ServiceCategoryHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin/service-categories").RequireAuthorization("AdminOnly").WithTags("Service categories - Danh mục dịch vụ");
        admin.MapPost("/filter", async (ServiceCategoryFilterQuery query, IServiceCategoryAppService svc) => (await svc.FilterAsync(query)).CustomResult())
            .WithSummary("Lọc danh mục động, sortBy=sortOrder|name|code; page/take, keyword và isActive.").WithOpenApi();
        admin.MapGet("/{id:guid}", async (Guid id, IServiceCategoryAppService svc) => (await svc.GetAsync(id)).CustomResult())
            .WithSummary("Chi tiết/ảnh hưởng danh mục: version, số dịch vụ/đề xuất và capability.").WithOpenApi();
        admin.MapPost("/", async (SaveServiceCategoryCommand command, IServiceCategoryAppService svc) => (await svc.SaveAsync(null, command)).CustomResult())
            .WithSummary("Tạo danh mục, UUID do BE cấp; code bất biến, operationId bắt buộc.").WithOpenApi();
        admin.MapPut("/{id:guid}", async (Guid id, SaveServiceCategoryCommand command, IServiceCategoryAppService svc) => (await svc.SaveAsync(id, command)).CustomResult())
            .WithSummary("Sửa danh mục với expectedVersion/operationId; không sửa đơn lịch sử.").WithOpenApi();
        admin.MapPut("/{id:guid}/status", async (Guid id, ChangeServiceCategoryCommand command, IServiceCategoryAppService svc) => (await svc.ChangeAsync(id, command, false)).CustomResult())
            .WithSummary("Khóa/mở hiệu lực danh mục, giữ cờ khóa riêng của dịch vụ con.").WithOpenApi();
        admin.MapPost("/{id:guid}/delete", async (Guid id, ChangeServiceCategoryCommand command, IServiceCategoryAppService svc) => (await svc.ChangeAsync(id, command, true)).CustomResult())
            .WithSummary("Soft delete, chặn nếu còn dịch vụ hoặc đề xuất đang chờ; không cascade.").WithOpenApi();
        admin.MapPost("/batch/status", async (AdministrativeBatchCommand command, IServiceCategoryAppService svc) => { command.Action = "Status"; return (await svc.BatchAsync(command)).CustomResult(); })
            .WithSummary("Khóa/mở 1–100 danh mục, kết quả từng ID, operationId bền vững; isActive đích bắt buộc.").WithOpenApi();
        admin.MapPost("/batch/delete", async (AdministrativeBatchCommand command, IServiceCategoryAppService svc) => { command.Action = "Delete"; return (await svc.BatchAsync(command)).CustomResult(); })
            .WithSummary("Xóa theo từng ID/version, giữ kết quả đã commit khi retry cùng operationId.").WithOpenApi();
        admin.MapGet("/operations/{id:guid}", async (Guid id, IServiceCategoryAppService svc) => (await svc.GetOperationAsync(id)).CustomResult())
            .WithSummary("Khôi phục kết quả batch của Admin hiện tại; nếu Processing gửi lại cùng payload/key để tiếp tục.").WithOpenApi();
        app.MapGet("/admin/lookups/service-categories", async (string? keyword, int? page, int? take, bool? includeInactive, IServiceCategoryAppService svc) =>
            (await svc.LookupAsync(keyword, page ?? 1, take ?? 20, includeInactive ?? false)).CustomResult()).RequireAuthorization("AdminOnly")
            .WithSummary("Lookup danh mục Admin, cho phép includeInactive khi lọc quản trị.").WithOpenApi();
        app.MapGet("/merchant/service-categories", async (string? keyword, int? page, int? take, IServiceCategoryAppService svc) =>
            (await svc.LookupAsync(keyword, page ?? 1, take ?? 20, false)).CustomResult()).RequireAuthorization("MerchantOnly")
            .WithSummary("Merchant chọn danh mục active cho đề xuất, không được CRUD.").WithOpenApi();
        app.MapGet("/customer/service-categories", async (string? keyword, int? page, int? take, IServiceCategoryAppService svc) =>
            (await svc.LookupAsync(keyword, page ?? 1, take ?? 20, false)).CustomResult()).AllowAnonymous().RequireRateLimiting("public-catalog")
            .WithSummary("Danh mục công khai active, phân trang và số dịch vụ khả dụng; UUID không phải ServiceType.").WithOpenApi();
    }
}
