using System.Data;
using System.Net;
using System.Text.Json;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Auditing;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class MyShipperTaskAppService(IRepository<DeliveryTask, Guid> tasks, IShipperRepository shippers,
    IWashOrderRepository orders, IWashOrderAppService orderService, ShipperDeliveryGuard guard, ShipperTaskPresentation presentation) : WashGoAppService, IMyShipperTaskAppService
{
    private async Task<IQueryable<DeliveryTask>> MineAsync()
    {
        var userId = CurrentUser.Id ?? Guid.Empty;
        var ids = (await shippers.GetQueryableAsync()).Where(x => x.UserId == userId && x.IsActive).Select(x => x.Id);
        return (await tasks.GetQueryableAsync()).Where(x => ids.Contains(x.ShipperId));
    }

    public async Task<BaseResponse<ShipperOperationDto>> GetOperationAsync(Guid taskId, Guid operationId)
    {
        // Historical operation ownership survives reassignment. Never reveal whether another actor used an operation ID.
        var ownedShipperIds = (await shippers.GetQueryableAsync()).Where(x => x.UserId == CurrentUser.Id).Select(x => x.Id);
        var task = await AsyncExecuter.FirstOrDefaultAsync((await tasks.GetQueryableAsync()).Where(x => x.Id == taskId && ownedShipperIds.Contains(x.ShipperId)));
        if (task == null) return NotFound<ShipperOperationDto>("Không tìm thấy nhiệm vụ.");
        var operation = await guard.FindAsync(taskId, operationId, CurrentUser.Id ?? Guid.Empty);
        return Success(new ShipperOperationDto { OperationId = operationId, TaskId = taskId,
            State = operation == null ? "unknown" : "succeeded",
            Result = operation?.ResultJson == null ? null : JsonSerializer.Deserialize<WashOrderDetailDto>(operation.ResultJson) });
    }

    private static string? Validate(MyShipperTaskQuery query)
    {
        query.SortBy ??= [];
        if (query.Page < 1 || query.Take is < 1 or > 100 || query.Keyword?.Length > 200) return "Bộ lọc/phân trang không hợp lệ.";
        if ((long)(query.Page - 1) * query.Take > int.MaxValue) return "Trang vượt giới hạn xử lý.";
        if (query.TaskType.HasValue && !Enum.IsDefined(query.TaskType.Value) || query.TaskStatus.HasValue && !Enum.IsDefined(query.TaskStatus.Value)) return "Loại/trạng thái nhiệm vụ không hợp lệ.";
        if (query.AssignedFrom < 0 || query.AssignedTo < 0 || query.AssignedFrom > BaseFilterQuery.MaxUnixSeconds || query.AssignedTo >= BaseFilterQuery.MaxUnixSeconds ||
            query.AssignedTo > 0 && query.AssignedFrom > query.AssignedTo) return "Khoảng ngày phân công không hợp lệ.";
        if (query.AssignedFrom > 0 && query.AssignedTo > 0 && query.AssignedTo - query.AssignedFrom + 1 > 366L * 86400) return "Khoảng phân công tối đa 366 ngày.";
        if (query.SortBy.Length > 2 || query.SortBy.Any(x => x == null || !new[] { "AssignedAt", "Deadline" }.Contains(x.Field, StringComparer.OrdinalIgnoreCase))) return "Chỉ sắp xếp AssignedAt/Deadline.";
        return null;
    }

    private async Task<IQueryable<DeliveryTask>> FilterAsync(MyShipperTaskQuery query, bool summary = false)
    {
        var rows = await MineAsync();
        var orderQuery = await orders.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var key = query.Keyword.Trim().ToLower();
            var ids = orderQuery.Where(x => x.OrderCode.ToLower().Contains(key) || x.MerchantOrderCode != null && x.MerchantOrderCode.ToLower().Contains(key)).Select(x => x.Id);
            rows = rows.Where(x => ids.Contains(x.OrderId));
        }
        if (query.AssignedFrom > 0) { var from = DateTimeOffset.FromUnixTimeSeconds(query.AssignedFrom).UtcDateTime; rows = rows.Where(x => x.AssignedAt >= from); }
        if (query.AssignedTo > 0) { var to = DateTimeOffset.FromUnixTimeSeconds(query.AssignedTo).UtcDateTime.AddSeconds(1); rows = rows.Where(x => x.AssignedAt < to); }
        if (!summary && query.TaskType.HasValue) rows = rows.Where(x => x.TaskType == query.TaskType);
        var status = summary ? DeliveryTaskStatus.Assigned : query.TaskStatus;
        if (status.HasValue) rows = rows.Where(x => x.Status == status);
        // Assigned rows must also agree with the current order; an orphaned old assignment is not actionable backlog.
        var active = DeliveryTaskRules.ActiveAssignments(await tasks.GetQueryableAsync(), orderQuery).Select(x => x.Id);
        rows = rows.Where(x => x.Status != DeliveryTaskStatus.Assigned || active.Contains(x.Id));
        return rows;
    }

    public async Task<PaginatedResult<MyShipperTaskDto>> GetListAsync(MyShipperTaskQuery query)
    {
        if (Validate(query) is { } error) return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error };
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
        var rows = await FilterAsync(query);
        var total = await AsyncExecuter.CountAsync(rows);
        IOrderedQueryable<DeliveryTask>? sorted = null;
        foreach (var sort in query.SortBy)
        {
            if (sort.Field.Equals("Deadline", StringComparison.OrdinalIgnoreCase))
                sorted = sorted == null ? sort.IsDescending ? rows.OrderByDescending(x => x.Deadline) : rows.OrderBy(x => x.Deadline)
                    : sort.IsDescending ? sorted.ThenByDescending(x => x.Deadline) : sorted.ThenBy(x => x.Deadline);
            else sorted = sorted == null ? sort.IsDescending ? rows.OrderByDescending(x => x.AssignedAt) : rows.OrderBy(x => x.AssignedAt)
                    : sort.IsDescending ? sorted.ThenByDescending(x => x.AssignedAt) : sorted.ThenBy(x => x.AssignedAt);
        }
        var data = await AsyncExecuter.ToListAsync((sorted ?? rows.OrderBy(x => x.AssignedAt)).ThenBy(x => x.Id).Skip((query.Page - 1) * query.Take).Take(query.Take));
        var list = await presentation.MapAsync(data);
        await uow.CompleteAsync();
        return Success(list, total, query.Page, query.Take);
    }

    public async Task<BaseResponse<ShipperTaskSummaryDto>> GetSummaryAsync(MyShipperTaskQuery query)
    {
        if (Validate(query) is { } error) return BadRequest<ShipperTaskSummaryDto>(error);
        var rows = await FilterAsync(query, summary: true);
        var counts = await AsyncExecuter.FirstOrDefaultAsync(rows.GroupBy(x => 1).Select(g => new { Total = g.Count(), Collect = g.Count(x => x.TaskType == DeliveryTaskType.Collect) }));
        return Success(new ShipperTaskSummaryDto { TotalPending = counts?.Total ?? 0, CollectPending = counts?.Collect ?? 0,
            ReturnPending = (counts?.Total ?? 0) - (counts?.Collect ?? 0), GeneratedAt = DateTime.UtcNow,
            Keyword = query.Keyword, AssignedFrom = query.AssignedFrom, AssignedTo = query.AssignedTo });
    }

    public async Task<BaseResponse<MyShipperTaskDto>> GetAsync(Guid taskId)
    {
        var task = await AsyncExecuter.FirstOrDefaultAsync((await MineAsync()).Where(x => x.Id == taskId));
        if (task == null) return NotFound<MyShipperTaskDto>("Không tìm thấy nhiệm vụ.");
        var dto = (await presentation.MapAsync([task])).Single();
        // Only current assignments may use the order detail endpoint; old tasks expose snapshots, not a new assignee's order.
        if (dto.CanCollect || dto.CanReturn)
        {
            var detail = await orderService.GetByIdAsync(task.OrderId);
            if (detail.Succeeded) dto.Order = detail.Data;
        }
        return Success(dto);
    }

    [DisableAuditing]
    public async Task<BaseResponse<ShipperTaskCredentialDto>> GetCredentialsAsync(Guid taskId)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var task = await AsyncExecuter.FirstOrDefaultAsync((await MineAsync()).Where(x => x.Id == taskId));
        if (task == null) return NotFound<ShipperTaskCredentialDto>("Không tìm thấy nhiệm vụ.");
        var order = await orders.GetForUpdateAsync(task.OrderId);
        if (order == null || !await tasks.AnyAsync(x => x.Id == taskId && x.Status == DeliveryTaskStatus.Assigned) || order.ShipperId != task.ShipperId || order.AssignedTaskType != task.TaskType ||
            !DeliveryTaskRules.IsActive(true, order.Status, order.AssignedTaskType, order.CancelledGoodsStatus))
            return new() { Succeeded = false, StatusCode = HttpStatusCode.Conflict, Message = "Nhiệm vụ không còn hoạt động.", Meta = new { code = "AssignmentChanged" } };
        await Audit.LogAsync(new AuditEntry("Shipper", "ViewTaskCredential", "DeliveryTask", task.Id.ToString(), null));
        var result = new ShipperTaskCredentialDto { TaskId = task.Id, OrderRevision = order.Revision,
            PinCode = order.ShipperTaskPinCode ?? string.Empty, QrCode = order.ShipperTaskQrString ?? string.Empty,
            RemainingAttempts = order.TaskPinLockoutUntil <= DateTime.UtcNow ? 5 : Math.Max(0, 5 - order.TaskPinAttemptCount),
            LockoutUntil = order.TaskPinLockoutUntil > DateTime.UtcNow ? order.TaskPinLockoutUntil : null, ServerNow = DateTime.UtcNow };
        await uow.CompleteAsync();
        return Success(result);
    }

}
