using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>Bổ sung cho danh sách/chi tiết đơn những cột đặc tả yêu cầu mà bảng đơn không lưu sẵn: dịch vụ, kích thước ô, cờ đơn mới.</summary>
    public class OrderListEnricher : ITransientDependency
    {
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<LockerBox, Guid> _boxRepo;
        private readonly IAsyncQueryableExecuter _executer;

        public OrderListEnricher(IRepository<WashOrderItem, Guid> itemRepo, IRepository<LockerBox, Guid> boxRepo,
            IAsyncQueryableExecuter executer)
        {
            _itemRepo = itemRepo;
            _boxRepo = boxRepo;
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

        // UC 2.3.1 step 4: the customer has deposited and the Merchant has not assigned a Shipper yet.
        private static bool IsNew(WashOrderResultDto order, string role) => role switch
        {
            "Merchant" => order.Status == WashOrderStatus.Deposited && order.ShipperId == null,
            _ => false
        };
    }
}
