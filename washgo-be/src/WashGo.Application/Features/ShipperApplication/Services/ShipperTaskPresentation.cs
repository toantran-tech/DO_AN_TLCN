using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Features.FileApplication.Services;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class ShipperTaskPresentation(IWashOrderRepository orders, ILockerRepository lockers, IRepository<OrderStorageCycle, Guid> cycles, IRepository<StorageReturnRequest, Guid> requests) : ITransientDependency
{
    public async Task<List<MyShipperTaskDto>> MapAsync(List<DeliveryTask> data)
    {
        var orderIds = data.Select(x => x.OrderId).ToList();
        var current = (await orders.GetListAsync(x => orderIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var cycleIds = data.Where(x => x.StorageCycleId.HasValue).Select(x => x.StorageCycleId!.Value).Distinct().ToList();
        var storageCycles = (await cycles.GetListAsync(x => cycleIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var targets = (await requests.GetListAsync(x => cycleIds.Contains(x.CycleId)))
            .GroupBy(x => x.CycleId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.RequestedAt).First());
        var lockerIds = current.Values.SelectMany(x => new[] { x.LockerId, x.ReturnLockerId ?? x.LockerId }).Concat(storageCycles.Values.Where(x => x.SourceLockerId.HasValue).Select(x => x.SourceLockerId!.Value))
            .Concat(targets.Values.Select(x => x.LockerId)).Distinct().ToList();
        var stations = (await lockers.GetListAsync(x => lockerIds.Contains(x.Id))).ToDictionary(x => x.Id);
        return data.Select(task =>
        {
            current.TryGetValue(task.OrderId, out var order);
            var isAssigned = task.Status == DeliveryTaskStatus.Assigned && order?.ShipperId == task.ShipperId && order.AssignedTaskType == task.TaskType;
            var active = isAssigned && DeliveryTaskRules.IsActive(true, order!.Status, order.AssignedTaskType, order.CancelledGoodsStatus);
            var collect = task.TaskType == DeliveryTaskType.Collect;
            var cycle = task.StorageCycleId.HasValue ? storageCycles.GetValueOrDefault(task.StorageCycleId.Value) : null;
            var target = cycle == null ? null : targets.GetValueOrDefault(cycle.Id);
            var stationId = task.TaskType == DeliveryTaskType.StorageTransfer && isAssigned ? cycle?.SourceLockerId
                : task.TaskType == DeliveryTaskType.StorageReturn && isAssigned ? target?.LockerId : !isAssigned ? task.LockerIdSnapshot : collect ? order!.LockerId : order!.ReturnLockerId ?? order.LockerId;
            var station = stationId.HasValue ? stations.GetValueOrDefault(stationId.Value) : null;
            var name = isAssigned ? station?.Name : task.LockerNameSnapshot;
            var address = isAssigned && station != null ? string.Join(", ", new[] { station.Address, station.Ward, station.District, station.City }.Where(x => !string.IsNullOrWhiteSpace(x))) : task.LockerAddressSnapshot;
            return new MyShipperTaskDto
            {
                TaskId = task.Id, OrderId = task.OrderId, OrderCode = task.OrderCodeSnapshot ?? order?.OrderCode ?? string.Empty,
                MerchantOrderCode = task.MerchantOrderCodeSnapshot ?? order?.MerchantOrderCode, TaskType = task.TaskType, Status = task.Status,
                AssignedAt = task.AssignedAt, CompletedAt = task.CompletedAt, EndedAt = task.EndedAt,
                OrderStatus = order?.Status ?? task.OrderStatusSnapshot ?? default, OrderRevision = order?.Revision ?? 0,
                LockerName = name ?? string.Empty, TaskLockerName = name, TaskLockerAddress = address,
                TaskLockerLatitude = isAssigned ? station?.Latitude : task.LockerLatitudeSnapshot,
                TaskLockerLongitude = isAssigned ? station?.Longitude : task.LockerLongitudeSnapshot,
                TaskLockerImageUrls = StoredImageUrls.Read(station?.Images),
                BoxNumber = isAssigned ? collect ? order!.BoxNumber : order!.ReturnBoxNumber : task.BoxNumberSnapshot,
                TaskBoxNumber = isAssigned ? collect ? order!.BoxNumber : order!.ReturnBoxNumber : task.BoxNumberSnapshot,
                Deadline = task.Deadline, SlaStartedAt = task.SlaStartedAt, SlaHours = task.SlaHours, ServerNow = DateTime.UtcNow,
                SlaUnavailableReason = task.Deadline.HasValue ? null : task.TaskType == DeliveryTaskType.Return &&
                    (order?.Status == WashOrderStatus.Cancelled || task.OrderStatusSnapshot == WashOrderStatus.Cancelled)
                    ? "CancelledReturnSlaNotDefined" : "MissingSlaSource",
                StorageCycleId = task.StorageCycleId, StorageShopAddress = cycle?.ShopAddress,
                CanStorageCollect = active && task.TaskType == DeliveryTaskType.StorageTransfer && cycle?.State == StorageCycleState.AwaitingCollection,
                CanStorageHandover = active && task.TaskType == DeliveryTaskType.StorageTransfer && cycle?.State == StorageCycleState.InboundTransit,
                CanStorageDepart = active && task.TaskType == DeliveryTaskType.StorageReturn && cycle?.State == StorageCycleState.Stored,
                CanStoragePlace = active && task.TaskType == DeliveryTaskType.StorageReturn && cycle?.State == StorageCycleState.OutboundTransit,
                CanCollect = active && collect, CanReturn = active && task.TaskType == DeliveryTaskType.Return, ReasonCode = active ? null : "TaskNotActive",
                MerchantName = task.MerchantNameSnapshot ?? order?.MerchantName, Note = isAssigned ? order?.ShipperNote : task.CompletionNote
            };
        }).ToList();
    }

}
