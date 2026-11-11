using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Application.Features.SystemSettingApplication.Services;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>Ghi các mốc của nhiệm vụ lấy/trả đồ, trong cùng transaction với thay đổi của đơn.</summary>
    public class DeliveryTaskRecorder : ITransientDependency
    {
        private readonly IRepository<DeliveryTask, Guid> _tasks;
        private readonly ILockerRepository _lockers;
        private readonly SystemSettingProvider _settings;

        public DeliveryTaskRecorder(IRepository<DeliveryTask, Guid> tasks, ILockerRepository lockers, SystemSettingProvider settings)
        {
            _tasks = tasks;
            _lockers = lockers;
            _settings = settings;
        }

        public async Task AssignedAsync(WashOrder order, Guid shipperId, DeliveryTaskType type, Guid? assignedBy, DateTime at)
        {
            await EndOpenAsync(order.Id, DeliveryTaskStatus.Reassigned, at);
            var task = new DeliveryTask(Guid.NewGuid())
            {
                OrderId = order.Id, ShipperId = shipperId, MerchantId = order.MerchantId, TaskType = type,
                Status = DeliveryTaskStatus.Assigned, AssignedAt = at, AssignedBy = assignedBy
            };
            await CaptureSlaAsync(task, order);
            await CaptureLocationAsync(task, order);
            task.SnapshotSource = "CapturedAtAssignment";
            await _tasks.InsertAsync(task, autoSave: true);
        }

        /// <summary>Closes the open task of this type; an assignment from before the table existed gets a row with no assignment time.</summary>
        public async Task CompletedAsync(WashOrder order, DeliveryTaskType type, WashOrderTimeline entry)
        {
            if (order.ShipperId is not { } shipperId)
                return;
            var open = await _tasks.FirstOrDefaultAsync(x => x.OrderId == order.Id && x.Status == DeliveryTaskStatus.Assigned &&
                                                             x.TaskType == type && x.ShipperId == shipperId);
            if (open == null)
            {
                open = new DeliveryTask(Guid.NewGuid())
                {
                    OrderId = order.Id, ShipperId = shipperId, MerchantId = order.MerchantId, TaskType = type,
                    AssignedAt = order.ShipperAssignedAt
                };
                await _tasks.InsertAsync(open, autoSave: false);
            }
            await CaptureSlaAsync(open, order);
            await CaptureLocationAsync(open, order);
            open.Status = DeliveryTaskStatus.Completed;
            open.CompletedAt = DateTimeOffset.FromUnixTimeSeconds(entry.Timestamp).UtcDateTime;
            open.CompletionEventId = entry.Id;
            open.CompletedBy = entry.ActorId;
            open.CompletedByRole = entry.ActorRole;
            open.SnapshotSource = "CapturedAtCompletion";
            open.OrderStatusSnapshot = entry.Status;
            open.CompletionNote = entry.Note;
            await _tasks.UpdateAsync(open, autoSave: true);
        }

        private async Task CaptureLocationAsync(DeliveryTask task, WashOrder order)
        {
            var collect = task.TaskType == DeliveryTaskType.Collect;
            var lockerId = collect ? order.LockerId : order.ReturnLockerId ?? order.LockerId;
            var locker = await _lockers.FindAsync(lockerId);
            task.OrderCodeSnapshot = order.OrderCode;
            task.MerchantOrderCodeSnapshot = order.MerchantOrderCode;
            task.MerchantNameSnapshot = order.MerchantName;
            task.LockerIdSnapshot = lockerId;
            task.LockerNameSnapshot = locker?.Name ?? (collect ? order.LockerName : null);
            task.LockerAddressSnapshot = locker == null ? null : string.Join(", ",
                new[] { locker.Address, locker.Ward, locker.District, locker.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
            task.LockerLatitudeSnapshot = locker?.Latitude;
            task.LockerLongitudeSnapshot = locker?.Longitude;
            task.BoxIdSnapshot = collect ? order.BoxId : null;
            task.BoxNumberSnapshot = collect ? order.BoxNumber : null;
            // Return box is unknown at assignment and becomes authoritative only after the drop-off.
            if (!collect && (order.Status == WashOrderStatus.ReadyToReturn ||
                order.Status == WashOrderStatus.Cancelled && order.CancelledGoodsStatus == CancelledGoodsStatus.AtReturnLocker))
            {
                task.BoxIdSnapshot = order.ReturnBoxId;
                task.BoxNumberSnapshot = order.ReturnBoxNumber;
            }
        }

        private async Task CaptureSlaAsync(DeliveryTask task, WashOrder order)
        {
            if (task.Deadline.HasValue) return;
            var settings = await _settings.ForOrderAsync(order);
            var collect = task.TaskType == DeliveryTaskType.Collect;
            task.SlaStartedAt = collect ? order.DepositedAt : order.Status == WashOrderStatus.Cancelled ? null : order.PaidAt;
            task.SlaHours = collect ? settings.MerchantCollectSlaHours : settings.MerchantReturnSlaHours;
            task.Deadline = task.SlaStartedAt?.AddHours(task.SlaHours.Value);
        }

        public Task CancelledAsync(Guid orderId, DateTime at) => EndOpenAsync(orderId, DeliveryTaskStatus.Cancelled, at);

        private async Task EndOpenAsync(Guid orderId, DeliveryTaskStatus status, DateTime at)
        {
            var open = await _tasks.GetListAsync(x => x.OrderId == orderId && x.Status == DeliveryTaskStatus.Assigned);
            foreach (var task in open)
            {
                task.Status = status;
                task.EndedAt = at;
            }
            if (open.Count > 0)
                await _tasks.UpdateManyAsync(open, autoSave: true);
        }
    }
}
