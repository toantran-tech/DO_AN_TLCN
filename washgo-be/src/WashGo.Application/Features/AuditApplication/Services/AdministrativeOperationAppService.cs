using System.Net;
using System.Text.Json;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Features.AuditApplication.Services;

public class AdministrativeOperationAppService : WashGoAppService, IAdministrativeOperationAppService
{
    public async Task<BaseResponse<AdministrativeOperationDto>> GetAsync(string module, Guid id)
    {
        if (!CurrentUser.IsInRole("Admin") || !CurrentUser.Id.HasValue) return AdministrativeWriteGuard.Error<AdministrativeOperationDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được tra cứu thao tác quản trị.");
        var row = await Writes.FindAsync(id, CurrentUser.Id.Value, module);
        if (row == null) return NotFound<AdministrativeOperationDto>("Chưa có kết quả của bạn; 404 không chứng minh yêu cầu đang chạy đã thất bại.");
        var result = JsonSerializer.Deserialize<JsonElement>(row.ResponseJson);
        var processing = row.Action == "Batch" && result.TryGetProperty("data", out var data) && data.TryGetProperty("state", out var state) && state.GetString() == "Processing";
        return Success(new AdministrativeOperationDto { OperationId = row.Id, Action = row.Action, ResourceId = row.ResourceId, CompletedAt = processing ? null : row.CompletedAt, State = processing ? "Processing" : "Completed", Result = result });
    }
}
