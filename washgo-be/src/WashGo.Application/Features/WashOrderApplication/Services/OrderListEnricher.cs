using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>Bổ sung cho danh sách/chi tiết đơn những cột đặc tả yêu cầu mà bảng đơn không lưu sẵn: dịch vụ, kích thước ô, cờ đơn mới.</summary>
    public class OrderListEnricher : ITransientDependency
    {
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<LockerBox, Guid> _boxRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IAsyncQueryableExecuter _executer;

        public OrderListEnricher(IRepository<WashOrderItem, Guid> itemRepo, IRepository<LockerBox, Guid> boxRepo,
            ILockerRepository lockerRepo, IAsyncQueryableExecuter executer)
        {
            _itemRepo = itemRepo;
            _boxRepo = boxRepo;
            _lockerRepo = lockerRepo;
            _executer = executer;
        }

        public async Task FillAsync<T>(List<T> orders, string role) where T : WashOrderResultDto
        {
            if (orders.Count == 0)
                return;
            var orderIds = orders.Select(x => x.Id).ToList();
            var boxIds = orders.Select(x => x.BoxId).Distinct().ToList();
            var services = (await _executer.ToListAsync((await _itemRepo.GetQueryableAsync())
                    .Where(x => orderIds.Contains(x.WashOrderId))
                    .Select(x => new { x.WashOrderId, x.ServiceName })))
                .GroupBy(x => x.WashOrderId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ServiceName).Distinct().ToList());
            var sizes = (await _executer.ToListAsync((await _boxRepo.GetQueryableAsync())
                    .Where(x => boxIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Size })))
                .ToDictionary(x => x.Id, x => x.Size);

            foreach (var order in orders)
            {
                order.ServiceNames = services.GetValueOrDefault(order.Id) ?? [];
                order.BoxSize = sizes.TryGetValue(order.BoxId, out var size) ? size : null;
                order.IsNew = IsNew(order, role);
            }
        }

        /// <summary>UC 4.3.2: locker, địa chỉ, ô của nhiệm vụ đang gán và nút "Lấy đồ"/"Trả đồ".</summary>
        public async Task FillTaskAsync(WashOrderDetailDto order)
        {
            if (!order.AssignedTaskType.HasValue)
                return;
            var collect = order.AssignedTaskType == DeliveryTaskType.Collect;
            var lockerId = collect ? order.LockerId : order.ReturnLockerId ?? order.LockerId;
            var locker = await _lockerRepo.FindAsync(lockerId);
            order.TaskLockerName = locker?.Name ?? order.LockerName;
            order.TaskLockerAddress = locker == null
                ? null
                : string.Join(", ", new[] { locker.Address, locker.Ward, locker.District, locker.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
            // The return box is picked when the Shipper drops the goods off.
            order.TaskBoxNumber = collect ? order.BoxNumber : order.ReturnBoxNumber;
            order.NextAction = IsActiveTask(order) ? order.AssignedTaskType : null;
        }

        private static bool IsNew(WashOrderResultDto order, string role) => role switch
        {
            // UC 2.3.1 step 4: the customer has deposited and the Merchant has not assigned a Shipper yet.
            "Merchant" => order.Status == WashOrderStatus.Deposited && order.ShipperId == null,
            // UC 4.3.1 step 4: a collect/return task the Shipper has not done yet.
            "Shipper" => IsActiveTask(order),
            _ => false
        };

        private static bool IsActiveTask(WashOrderResultDto order) =>
            DeliveryTaskRules.IsActive(order.ShipperId.HasValue, order.Status, order.AssignedTaskType, order.CancelledGoodsStatus);
    }
}
