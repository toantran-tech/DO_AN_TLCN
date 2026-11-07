using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services
{
    /// <summary>
    /// UC 4.3.6: lịch sử giao nhận lấy từ timeline do chính Shipper ghi khi lấy đồ (Collected) và trả đồ
    /// (ReadyToReturn, hoặc trả đồ của đơn hủy) — giữ đúng cả khi đơn sau đó được phân công cho người khác.
    /// </summary>
    public class ShipperDeliveryAppService : WashGoAppService, IShipperDeliveryAppService
    {
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly ILockerRepository _lockerRepo;

        public ShipperDeliveryAppService(
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo)
        {
            _timelineRepo = timelineRepo;
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
        }

        public async Task<PaginatedResult<DeliveryHistoryDto>> GetHistoryAsync(DeliveryHistoryQuery query)
        {
            var userId = CurrentUser.Id ?? Guid.Empty;
            var entries = (await _timelineRepo.GetQueryableAsync())
                .Where(x => x.ActorId == userId && x.ActorRole == "Shipper" &&
                            (x.Status == WashOrderStatus.Collected || x.Status == WashOrderStatus.ReadyToReturn ||
                             x.Status == WashOrderStatus.Cancelled && x.FromStatus == WashOrderStatus.Cancelled));
            if (query.TaskType == DeliveryTaskType.Collect)
                entries = entries.Where(x => x.Status == WashOrderStatus.Collected);
            else if (query.TaskType == DeliveryTaskType.Return)
                entries = entries.Where(x => x.Status != WashOrderStatus.Collected);
            if (query.FromDate is > 0)
                entries = entries.Where(x => x.Timestamp >= query.FromDate.Value);
            if (query.ToDate is > 0)
                entries = entries.Where(x => x.Timestamp <= query.ToDate.Value);

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(entries);
            // Timestamps are whole seconds: a collect and a return in the same second keep the flow order (return first).
            var rows = await AsyncExecuter.ToListAsync(entries.OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.Status == WashOrderStatus.Collected ? 0 : 1).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));

            var orderIds = rows.Select(x => x.WashOrderId).Distinct().ToList();
            var orders = (await _orderRepo.GetListAsync(x => orderIds.Contains(x.Id))).ToDictionary(x => x.Id);
            var lockerIds = orders.Values.Where(x => x.ReturnLockerId.HasValue).Select(x => x.ReturnLockerId!.Value).Distinct().ToList();
            var lockerNames = (await _lockerRepo.GetListAsync(x => lockerIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);

            var list = new List<DeliveryHistoryDto>();
            foreach (var row in rows)
            {
                if (!orders.TryGetValue(row.WashOrderId, out var order))
                    continue;
                var collect = row.Status == WashOrderStatus.Collected;
                list.Add(new DeliveryHistoryDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    TaskType = collect ? DeliveryTaskType.Collect : DeliveryTaskType.Return,
                    LockerName = collect || !order.ReturnLockerId.HasValue
                        ? order.LockerName
                        : lockerNames.GetValueOrDefault(order.ReturnLockerId.Value, order.LockerName),
                    BoxNumber = collect ? order.BoxNumber : order.ReturnBoxNumber,
                    CompletedAt = DateTimeOffset.FromUnixTimeSeconds(row.Timestamp).UtcDateTime,
                    OrderStatus = order.Status
                });
            }
            return Success(list, total, page, take, total == 0 ? "Bạn chưa có lịch sử giao nhận." : null);
        }
    }
}
