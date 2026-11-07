using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>Ghi các mốc của nhiệm vụ lấy/trả đồ, trong cùng transaction với thay đổi của đơn.</summary>
    public class DeliveryTaskRecorder : ITransientDependency
    {
        private readonly IRepository<DeliveryTask, Guid> _tasks;

        public DeliveryTaskRecorder(IRepository<DeliveryTask, Guid> tasks) => _tasks = tasks;

        public async Task AssignedAsync(WashOrder order, Guid shipperId, DeliveryTaskType type, Guid? assignedBy, DateTime at)
        {
            await EndOpenAsync(order.Id, DeliveryTaskStatus.Reassigned, at);
            await _tasks.InsertAsync(new DeliveryTask(Guid.NewGuid())
            {
                OrderId = order.Id, ShipperId = shipperId, MerchantId = order.MerchantId, TaskType = type,
                Status = DeliveryTaskStatus.Assigned, AssignedAt = at, AssignedBy = assignedBy
            }, autoSave: true);
        }

        /// <summary>Closes the open task of this type; an assignment from before the table existed gets a row with no assignment time.</summary>
        public async Task CompletedAsync(WashOrder order, DeliveryTaskType type, DateTime at)
        {
            if (order.ShipperId is not { } shipperId)
                return;
            var open = await _tasks.FirstOrDefaultAsync(x => x.OrderId == order.Id && x.Status == DeliveryTaskStatus.Assigned &&
                                                             x.TaskType == type && x.ShipperId == shipperId);
            if (open == null)
            {
                await _tasks.InsertAsync(new DeliveryTask(Guid.NewGuid())
                {
                    OrderId = order.Id, ShipperId = shipperId, MerchantId = order.MerchantId, TaskType = type,
                    Status = DeliveryTaskStatus.Completed, AssignedAt = order.ShipperAssignedAt, CompletedAt = at
                }, autoSave: true);
                return;
            }
            open.Status = DeliveryTaskStatus.Completed;
            open.CompletedAt = at;
            await _tasks.UpdateAsync(open, autoSave: true);
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
