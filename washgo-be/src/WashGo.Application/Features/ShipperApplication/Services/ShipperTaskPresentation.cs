using System.Text.Json;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Features.FileApplication.Services;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class ShipperTaskPresentation(IWashOrderRepository orders, ILockerRepository lockers) : ITransientDependency
{
    public async Task<List<MyShipperTaskDto>> MapAsync(List<DeliveryTask> data)
    {
        var orderIds = data.Select(x => x.OrderId).ToList();
        var current = (await orders.GetListAsync(x => orderIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var lockerIds = current.Values.SelectMany(x => new[] { x.LockerId, x.ReturnLockerId ?? x.LockerId }).Distinct().ToList();
        var stations = (await lockers.GetListAsync(x => lockerIds.Contains(x.Id))).ToDictionary(x => x.Id);
        return data.Select(task =>
        {
            current.TryGetValue(task.OrderId, out var order);
            var isAssigned = task.Status == DeliveryTaskStatus.Assigned && order?.ShipperId == task.ShipperId && order.AssignedTaskType == task.TaskType;
            var active = isAssigned && DeliveryTaskRules.IsActive(true, order!.Status, order.AssignedTaskType, order.CancelledGoodsStatus);
            var collect = task.TaskType == DeliveryTaskType.Collect;
            var stationId = !isAssigned ? task.LockerIdSnapshot : collect ? order!.LockerId : order!.ReturnLockerId ?? order.LockerId;
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
                CanCollect = active && collect, CanReturn = active && !collect, ReasonCode = active ? null : "TaskNotActive",
                MerchantName = task.MerchantNameSnapshot ?? order?.MerchantName, Note = isAssigned ? order?.ShipperNote : task.CompletionNote
            };
        }).ToList();
    }

}
