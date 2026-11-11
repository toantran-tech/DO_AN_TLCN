using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Application.Features.FileApplication.Services;

namespace WashGo.Application.Features.ShipperApplication.Services;

/// <summary>One completion source shared by history, summary and dashboard; never joins to the current assignee.</summary>
public class ShipperHistoryReader(IRepository<DeliveryTask, Guid> tasks, ILockerRepository lockers, IWashOrderRepository orders,
    IAsyncQueryableExecuter executer) : ITransientDependency
{
    public async Task<IQueryable<DeliveryTask>> QueryAsync(Guid actor, DeliveryHistoryQuery query, bool ignoreType = false)
    {
        var rows = (await tasks.GetQueryableAsync()).Where(x => x.Status == DeliveryTaskStatus.Completed &&
            x.CompletedBy == actor && x.CompletedByRole == "Shipper" && x.CompletedAt != null && x.CompletionEventId != null);
        if (query.FromDate > 0) { var date = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime; rows = rows.Where(x => x.CompletedAt >= date); }
        if (query.ToDate > 0) { var date = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime.AddSeconds(1); rows = rows.Where(x => x.CompletedAt < date); }
        if (!ignoreType && query.TaskType.HasValue) rows = rows.Where(x => x.TaskType == query.TaskType);
        if (query.LockerId.HasValue) rows = rows.Where(x => x.LockerIdSnapshot == query.LockerId);
        if (query.EventStatus.HasValue) rows = rows.Where(x => x.OrderStatusSnapshot == query.EventStatus);
        if (query.CancelledReturn.HasValue) rows = query.CancelledReturn.Value
            ? rows.Where(x => x.TaskType == DeliveryTaskType.Return && x.OrderStatusSnapshot == WashOrderStatus.Cancelled)
            : rows.Where(x => x.OrderStatusSnapshot != WashOrderStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var key = query.Keyword.Trim().ToLower();
            rows = rows.Where(x => x.OrderCodeSnapshot != null && x.OrderCodeSnapshot.ToLower().Contains(key) ||
                x.MerchantOrderCodeSnapshot != null && x.MerchantOrderCodeSnapshot.ToLower().Contains(key));
        }
        return rows;
    }

    public static IOrderedQueryable<DeliveryTask> Sort(IQueryable<DeliveryTask> rows, DeliveryHistoryQuery query)
    {
        IOrderedQueryable<DeliveryTask>? sorted = null;
        foreach (var option in query.SortBy)
        {
            if (option.Field.Equals("OrderCode", StringComparison.OrdinalIgnoreCase))
                sorted = sorted == null ? option.IsDescending ? rows.OrderByDescending(x => x.OrderCodeSnapshot) : rows.OrderBy(x => x.OrderCodeSnapshot)
                    : option.IsDescending ? sorted.ThenByDescending(x => x.OrderCodeSnapshot) : sorted.ThenBy(x => x.OrderCodeSnapshot);
            else sorted = sorted == null ? option.IsDescending ? rows.OrderByDescending(x => x.CompletedAt) : rows.OrderBy(x => x.CompletedAt)
                    : option.IsDescending ? sorted.ThenByDescending(x => x.CompletedAt) : sorted.ThenBy(x => x.CompletedAt);
        }
        return (sorted ?? rows.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.TaskType)).ThenByDescending(x => x.CompletionEventId);
    }

    public async Task<List<DeliveryHistoryDto>> MapAsync(List<DeliveryTask> tasks)
    {
        var ids = tasks.Where(x => x.LockerIdSnapshot.HasValue).Select(x => x.LockerIdSnapshot!.Value).Distinct().ToList();
        var images = (await executer.ToListAsync((await lockers.GetQueryableAsync()).Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Images }))).ToDictionary(x => x.Id, x => StoredImageUrls.Read(x.Images));
        var orderIds = tasks.Select(x => x.OrderId).Distinct().ToList();
        var statuses = (await executer.ToListAsync((await orders.GetQueryableAsync()).Where(x => orderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Status }))).ToDictionary(x => x.Id, x => x.Status);
        return tasks.Select(x => new DeliveryHistoryDto
        {
            EventId = x.CompletionEventId!.Value, TaskId = x.Id, OrderId = x.OrderId, OrderCode = x.OrderCodeSnapshot ?? string.Empty,
            MerchantOrderCode = x.MerchantOrderCodeSnapshot, MerchantName = x.MerchantNameSnapshot, TaskType = x.TaskType,
            LockerId = x.LockerIdSnapshot, LockerName = x.LockerNameSnapshot ?? string.Empty, LockerAddress = x.LockerAddressSnapshot,
            LockerLatitude = x.LockerLatitudeSnapshot, LockerLongitude = x.LockerLongitudeSnapshot,
            LockerImageUrls = x.LockerIdSnapshot.HasValue ? images.GetValueOrDefault(x.LockerIdSnapshot.Value) ?? [] : [],
            BoxNumber = x.BoxNumberSnapshot, CompletedAt = x.CompletedAt!.Value, EventStatus = x.OrderStatusSnapshot,
            OrderStatus = statuses.TryGetValue(x.OrderId, out var status) ? status : x.OrderStatusSnapshot ?? default,
            CurrentOrderStatus = statuses.TryGetValue(x.OrderId, out var current) ? current : null,
            AssignedAt = x.AssignedAt, Deadline = x.Deadline, SlaHours = x.SlaHours,
            SnapshotSource = x.SnapshotSource, Note = x.CompletionNote
        }).ToList();
    }

}
