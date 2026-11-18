using System.Net;
using Volo.Abp;
using Volo.Abp.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Results;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.ServiceCategoryApplication.Services;

public class ServiceCategoryAppService(IRepository<ServiceCategoryEntity, Guid> categories,
    IRepository<WashService, Guid> services, IRepository<ServiceChangeRequest, Guid> proposals,
    AdministrativeWriteGuard guard, AdministrativeBatchExecutor batches) : WashGoAppService, IServiceCategoryAppService
{
    private const string Module = "ServiceCategory";
    private bool Admin => CurrentUser.Id.HasValue && CurrentUser.IsInRole("Admin");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BaseResponse<T> Fail<T>(string code, string message, HttpStatusCode status = HttpStatusCode.Conflict, T? data = default) => AdministrativeWriteGuard.Error(status, code, message, data);
    private static string Version(ServiceCategoryEntity row) => AdministrativeWriteGuard.Version(new { row.Id, row.Revision, row.IsDeleted });

    public Task<PaginatedResult<ServiceCategoryDto>> LookupAsync(string? keyword, int page, int take, bool includeInactive) =>
        ListAsync(new() { Keyword = keyword, Page = page, Take = take, IsActive = includeInactive && CurrentUser.IsInRole("Admin") ? null : true }, lookup: true);
    public Task<PaginatedResult<ServiceCategoryDto>> FilterAsync(ServiceCategoryFilterQuery query) => ListAsync(query, lookup: false);
    private async Task<PaginatedResult<ServiceCategoryDto>> ListAsync(ServiceCategoryFilterQuery filter, bool lookup)
    {
        if (!lookup && !Admin) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Admin được quản lý danh mục." };
        if (filter.Keyword?.Length > 200 || filter.Page is < 1 or > 100000 || filter.Take is < 1 or > 100 ||
            filter.SortBy is not ("sortOrder" or "name" or "code")) return new() { StatusCode = HttpStatusCode.BadRequest, Message = "Bộ lọc hoặc phân trang không hợp lệ." };
        var query = (await categories.GetQueryableAsync()).Where(x => !x.IsDeleted);
        if (filter.IsActive.HasValue) query = query.Where(x => x.IsActive == filter.IsActive);
        if (!Admin) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.Keyword)) { var term = ServiceCategoryRules.Key(filter.Keyword); query = query.Where(x => x.NormalizedName.Contains(term) || x.Code.Contains(term)); }
        var count = await AsyncExecuter.CountAsync(query);
        IOrderedQueryable<ServiceCategoryEntity> sorted = filter.SortBy switch
        {
            "name" => filter.IsDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "code" => filter.IsDescending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            _ => filter.IsDescending ? query.OrderByDescending(x => x.SortOrder) : query.OrderBy(x => x.SortOrder)
        };
        var rows = await AsyncExecuter.ToListAsync(sorted.ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((filter.Page - 1) * filter.Take).Take(filter.Take));
        var dtos = new List<ServiceCategoryDto>();
        foreach (var row in rows) dtos.Add(await DtoAsync(row, lookup));
        return Success(dtos, count, filter.Page, filter.Take);
    }
    public async Task<BaseResponse<ServiceCategoryDto>> GetAsync(Guid id)
    {
        if (!Admin) return Fail<ServiceCategoryDto>("AdminOnly", "Chỉ Admin được xem hồ sơ danh mục.", HttpStatusCode.Forbidden);
        var row = await categories.FindAsync(id);
        return row == null || row.IsDeleted ? NotFound<ServiceCategoryDto>("Không tìm thấy danh mục.") : Success(await DtoAsync(row));
    }
    private async Task<int> PendingAsync(ServiceCategoryEntity row)
    {
        var pending = await proposals.GetListAsync(x => x.Status == ServiceApprovalStatus.Pending);
        return pending.Count(p => References(p.ProposedData, row.Id));
    }
    private static bool References(string? json, Guid categoryId)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? "{}"); var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return true;
            if (root.TryGetProperty("categoryId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var explicitId)) return explicitId == categoryId;
            return root.TryGetProperty("serviceType", out var type) && type.TryGetInt32(out var value) && value is >= 1 and <= 5 && ServiceCategoryRules.LegacyId(value) == categoryId;
        }
        catch (JsonException) { return true; } // Unreadable pending proposals require review before destructive actions.
    }
    private async Task<ServiceCategoryDto> DtoAsync(ServiceCategoryEntity row, bool lookup = false)
    {
        var publicOnly = !CurrentUser.IsInRole("Admin") && !CurrentUser.IsInRole("Merchant");
        var query = (await services.GetQueryableAsync()).Where(x => !x.IsDeleted && x.CategoryId == row.Id);
        if (publicOnly) query = query.Where(x => x.IsActive && x.ApprovalStatus == ServiceApprovalStatus.Approved && x.UnitPrice > 0);
        var count = await AsyncExecuter.CountAsync(query);
        var pending = Admin && !lookup ? await PendingAsync(row) : 0;
        return new() { Id = row.Id, LegacyServiceType = Enumerable.Range(1,5).Where(x => ServiceCategoryRules.LegacyId(x) == row.Id).Select(x => (int?)x).FirstOrDefault(), Code = row.Code, Name = row.Name, Description = row.Description, IsActive = row.IsActive, SortOrder = row.SortOrder,
            Version = Admin ? Version(row) : null, ServiceCount = count, PendingProposalCount = pending,
            CanEdit = Admin, CanChangeStatus = Admin, CanDelete = Admin && count == 0 && pending == 0,
            DeleteReasonCode = Admin && (count > 0 || pending > 0) ? "CategoryInUse" : null };
    }

    [UnitOfWork(IsDisabled = true)]
    public Task<BaseResponse<ServiceCategoryDto>> SaveAsync(Guid? id, SaveServiceCategoryCommand command) => WriteAsync(id, "Save", command.OperationId, command.ExpectedVersion, command, async row =>
    {
        var error = ServiceCategoryRules.Validate(command.Name, command.Description, command.SortOrder);
        if (error != null) return Fail<ServiceCategoryDto>("InvalidCategory", error, HttpStatusCode.BadRequest);
        // The code is issued by the server: ignored on create, and on edit only the stored code is accepted.
        if (id.HasValue && !BusinessCodeRules.AcceptsOnUpdate(command.Code, row.Code))
            return Fail<ServiceCategoryDto>(BusinessCodeRules.ImmutableCode, BusinessCodeRules.ImmutableMessage, HttpStatusCode.BadRequest);
        var name = ServiceCategoryRules.Name(command.Name); var key = ServiceCategoryRules.Key(name);
        if (await categories.AnyAsync(x => x.Id != row.Id && !x.IsDeleted && x.NormalizedName == key))
            return Fail<ServiceCategoryDto>("CategoryDuplicate", "Tên danh mục đã tồn tại.");
        var changed = !id.HasValue || row.Name != name || row.Description != command.Description?.Trim() || row.IsActive != command.IsActive || row.SortOrder != command.SortOrder;
        if (!id.HasValue) row.Code = await LazyServiceProvider.LazyGetRequiredService<IBusinessCodeAllocator>().AllocateAsync(BusinessCodeRules.Category, row.Id);
        row.Name = name; row.NormalizedName = key; row.Description = command.Description?.Trim(); row.IsActive = command.IsActive; row.SortOrder = command.SortOrder;
        if (!id.HasValue) await categories.InsertAsync(row, autoSave: true);
        else if (changed) { row.Revision++; await categories.UpdateAsync(row, autoSave: true); }
        var response = Success(await DtoAsync(row));
        if (!changed) AdministrativeBatchExecutor.Unchanged(response);
        return response;
    });

    [UnitOfWork(IsDisabled = true)]
    public Task<BaseResponse<ServiceCategoryDto>> ChangeAsync(Guid id, ChangeServiceCategoryCommand command, bool delete) => WriteAsync(id, delete ? "Delete" : "Status", command.OperationId, command.ExpectedVersion, command, async row =>
    {
        var dto = await DtoAsync(row);
        if (delete && !dto.CanDelete) return Fail("CategoryInUse", "Chuyển dịch vụ hoặc xử lý đề xuất trước khi xóa danh mục.", data: dto);
        if (!delete && row.IsActive == command.IsActive) return AdministrativeBatchExecutor.Unchanged(Success(dto));
        row.IsDeleted = delete; row.IsActive = delete ? false : command.IsActive; row.Revision++;
        if (delete) { row.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); row.DeletedBy = CurrentUser.Id?.ToString(); }
        await categories.UpdateAsync(row, autoSave: true);
        return Success(await DtoAsync(row));
    });

    private async Task<BaseResponse<ServiceCategoryDto>> WriteAsync(Guid? id, string action, Guid operationId, string? expectedVersion, object payload,
        Func<ServiceCategoryEntity, Task<BaseResponse<ServiceCategoryDto>>> apply)
    {
        if (!Admin) return Fail<ServiceCategoryDto>("AdminOnly", "Chỉ Admin được ghi danh mục.", HttpStatusCode.Forbidden);
        if (operationId == Guid.Empty) return Fail<ServiceCategoryDto>("OperationIdRequired", "Cần operationId UUID.", HttpStatusCode.BadRequest);
        var reason = payload is SaveServiceCategoryCommand saveInput ? saveInput.Reason : (payload as ChangeServiceCategoryCommand)?.Reason;
        if (reason?.Length > 500) return Fail<ServiceCategoryDto>("ReasonTooLong", "Reason must not exceed 500 characters.", HttpStatusCode.BadRequest);
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        using var deleted = LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>();
        if (await guard.ReplayAsync<ServiceCategoryDto>(operationId, CurrentUser.Id!.Value, Module, action, id?.ToString() ?? "new", payload) is { } replay) return replay;
        await guard.LockAsync("service-catalog");
        var row = id.HasValue ? await categories.FindAsync(id.Value) : new ServiceCategoryEntity(Guid.NewGuid());
        var before = row == null ? null : new { row.Code, row.Name, row.Description, row.IsActive, row.SortOrder, row.Revision, row.IsDeleted };
        BaseResponse<ServiceCategoryDto> response;
        if (row == null || row.IsDeleted) response = NotFound<ServiceCategoryDto>("Không tìm thấy danh mục.");
        else if (id.HasValue && expectedVersion != Version(row)) response = Fail<ServiceCategoryDto>("VersionMismatch", "Danh mục đã đổi hoặc thiếu expectedVersion.", data: await DtoAsync(row));
        else response = await apply(row);
        var unchanged = response.Meta?.GetType().GetProperty("outcome")?.GetValue(response.Meta)?.ToString() == "Unchanged";
        if (response.Succeeded && !unchanged && row != null)
            await Audit.LogAsync(new AuditEntry(Module, action, "ServiceCategory", row.Id.ToString(), row.Code) {
                Reason = payload is SaveServiceCategoryCommand save ? save.Reason : (payload as ChangeServiceCategoryCommand)?.Reason,
                Details = new { Before = id.HasValue ? before : null, After = new { row.Code, row.Name, row.Description, row.IsActive, row.SortOrder, row.Revision, row.IsDeleted } } });
        // Failed item results are durable too: batch resume does not retry a previously failed item silently.
        await guard.RecordAsync(operationId, CurrentUser.Id!.Value, Module, action, id?.ToString() ?? "new", payload, response);
        await uow.CompleteAsync(); return response;
    }
    [UnitOfWork(IsDisabled = true)]
    public Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command) => command.Action == "Status" && !command.IsActive.HasValue
        ? Task.FromResult(Fail<AdministrativeBatchResultDto>("TargetStatusRequired", "isActive is required.", HttpStatusCode.BadRequest)) : batches.RunAsync(Module, command, 100,
        async (item, key) => {
            var response = await ChangeAsync(item.Id, new() { OperationId = key, ExpectedVersion = item.ExpectedVersion,
                IsActive = command.IsActive ?? false, Reason = command.Reason }, command.Action == "Delete");
            return BatchItem(item.Id, response);
        });
    private static AdministrativeBatchItemResultDto BatchItem(Guid id, BaseResponse<ServiceCategoryDto> response)
    {
        var item = AdministrativeBatchExecutor.Item(id, response);
        if (response.Data != null) {
            item.Version = response.Data.Version;
            item.AffectedServiceCount = response.Data.ServiceCount;
            item.DependencyCounts = new() { ["services"] = response.Data.ServiceCount, ["pendingProposals"] = response.Data.PendingProposalCount };
        }
        return item;
    }
    public async Task<BaseResponse<AdministrativeBatchResultDto>> GetOperationAsync(Guid id)
    {
        if (!Admin) return Fail<AdministrativeBatchResultDto>("AdminOnly", "Chỉ Admin được tra operation.", HttpStatusCode.Forbidden);
        var row = await guard.FindAsync(id, CurrentUser.Id!.Value, Module);
        if (row == null || row.Action != "Batch") return NotFound<AdministrativeBatchResultDto>("Operation not found.");
        var response = JsonSerializer.Deserialize<BaseResponse<AdministrativeBatchResultDto>>(row.ResponseJson, Json) ?? throw new InvalidOperationException("Invalid stored batch.");
        if (response.Data?.State == "Processing")
        {
            for (var i = 0; i < response.Data.Items.Count; i++) {
                var item = response.Data.Items[i];
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}:{item.Id:N}"));
                var child = await guard.FindAsync(new Guid(bytes.AsSpan(0,16)), CurrentUser.Id!.Value, Module);
                if (child != null) response.Data.Items[i] = BatchItem(item.Id, JsonSerializer.Deserialize<BaseResponse<ServiceCategoryDto>>(child.ResponseJson, Json)!);
            }
        }
        return response;
    }
}
