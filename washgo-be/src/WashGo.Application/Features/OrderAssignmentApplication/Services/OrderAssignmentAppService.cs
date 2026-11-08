using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Commands;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Queries;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Results;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Services;
using WashGo.Application.Features.LockerApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.OrderAssignmentApplication.Services
{
    /// <summary>
    /// UC 3.3: đơn tạo ra chưa có Merchant; sau khi khách gửi đồ, Admin chọn một trong các Merchant phụ trách locker
    /// (thủ công hoặc theo gợi ý). Chỉ đơn đã gửi đồ và chưa có Merchant mới được phân công.
    /// </summary>
    public class OrderAssignmentAppService : WashGoAppService, IOrderAssignmentAppService
    {
        private const int MaxBatch = 50;

        // Orders a Merchant is still responsible for, counted as its current workload.
        private static readonly WashOrderStatus[] LoadStatuses =
        [
            WashOrderStatus.Deposited, WashOrderStatus.Collected, WashOrderStatus.Washing, WashOrderStatus.Drying,
            WashOrderStatus.Ironing, WashOrderStatus.Folding, WashOrderStatus.AwaitingPayment, WashOrderStatus.Paid,
            WashOrderStatus.ReadyToReturn
        ];

        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IRepository<LockerBox, Guid> _boxRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<ServiceArea, Guid> _areaRepo;
        private readonly LockerMerchantResolver _lockerMerchants;
        private readonly NotificationPublisher _notifications;

        public OrderAssignmentAppService(
            IWashOrderRepository orderRepo,
            IRepository<WashOrderItem, Guid> itemRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            ILockerRepository lockerRepo,
            IRepository<LockerBox, Guid> boxRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<ServiceArea, Guid> areaRepo,
            LockerMerchantResolver lockerMerchants,
            NotificationPublisher notifications)
        {
            _orderRepo = orderRepo;
            _itemRepo = itemRepo;
            _timelineRepo = timelineRepo;
            _lockerRepo = lockerRepo;
            _boxRepo = boxRepo;
            _merchantRepo = merchantRepo;
            _areaRepo = areaRepo;
            _lockerMerchants = lockerMerchants;
            _notifications = notifications;
        }

        // UC 3.3.1
        public async Task<PaginatedResult<WaitingOrderDto>> GetWaitingOrdersAsync(WaitingOrderFilterQuery query)
        {
            var now = DateTime.UtcNow;
            var overdueBefore = now.AddMinutes(-OrderAssignmentRules.AssignmentOverdueMinutes);
            var lockers = await _lockerRepo.GetQueryableAsync();
            var queryable = (await _orderRepo.GetQueryableAsync())
                .Where(o => !o.IsDeleted && o.Status == WashOrderStatus.Deposited && o.MerchantId == null);
            if (query.OverdueOnly == true)
                queryable = queryable.Where(o => o.DepositedAt <= overdueBefore);
            if (!string.IsNullOrWhiteSpace(query.District))
                queryable = queryable.Where(o => lockers.Any(l => l.Id == o.LockerId && l.District == query.District));
            if (!string.IsNullOrWhiteSpace(query.City))
                queryable = queryable.Where(o => lockers.Any(l => l.Id == o.LockerId && l.City == query.City));
            if (query.ServiceAreaId.HasValue)
                queryable = queryable.Where(o => lockers.Any(l => l.Id == o.LockerId && l.ServiceAreaId == query.ServiceAreaId));
            if (query.FromDate is > 0)
            {
                var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate.Value).UtcDateTime;
                queryable = queryable.Where(o => o.DepositedAt >= from);
            }
            if (query.ToDate is > 0)
            {
                var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate.Value).UtcDateTime;
                queryable = queryable.Where(o => o.DepositedAt <= to);
            }

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var orders = await AsyncExecuter.ToListAsync(queryable.OrderBy(o => o.DepositedAt).ThenBy(o => o.Id).Skip((page - 1) * take).Take(take));
            return Success(await ToWaitingDtosAsync(orders, overdueBefore, now), total, page, take,
                total == 0 ? "Không có đơn hàng nào chờ phân công." : null);
        }

        // UC 3.3.2 step 4
        public async Task<BaseResponse<OrderCandidatesDto>> GetCandidatesAsync(Guid orderId)
        {
            var now = DateTime.UtcNow;
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || order.IsDeleted)
                return NotFound<OrderCandidatesDto>("Không tìm thấy đơn hàng!");
            var candidates = await CandidatesAsync(order);
            return Success(new OrderCandidatesDto
            {
                Order = (await ToWaitingDtosAsync([order], now.AddMinutes(-OrderAssignmentRules.AssignmentOverdueMinutes), now))[0],
                Merchants = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Priority).ToList()
            }, candidates.Count == 0 ? OrderAssignmentRules.NoMerchantInArea : null);
        }

        // UC 3.3.2 steps 5-10
        public async Task<BaseResponse<AssignmentResultDto>> AssignAsync(Guid orderId, AssignOrderCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var (result, notFound, error) = await AssignCoreAsync(orderId, command.MerchantId, false);
            if (result == null)
                return notFound ? NotFound<AssignmentResultDto>("Không tìm thấy đơn hàng!") : BadRequest<AssignmentResultDto>(error!);
            await uow.CompleteAsync();
            return Success(result, "Phân công thành công!");
        }

        // UC 3.3.3 steps 4-5
        public async Task<BaseResponse<List<AutoSuggestionDto>>> SuggestAsync(AutoSuggestCommand command)
        {
            var ids = (command.OrderIds ?? []).Distinct().ToList();
            if (ids.Count == 0 || ids.Count > MaxBatch)
                return BadRequest<List<AutoSuggestionDto>>($"Vui lòng chọn từ 1 đến {MaxBatch} đơn hàng.");
            var orders = await _orderRepo.GetListAsync(o => ids.Contains(o.Id) && !o.IsDeleted);
            // Orders suggested earlier in this batch count toward each Merchant's load.
            var extraLoad = new Dictionary<Guid, int>();
            var result = new List<AutoSuggestionDto>();
            foreach (var id in ids)
            {
                var order = orders.FirstOrDefault(o => o.Id == id);
                if (order == null)
                    return NotFound<List<AutoSuggestionDto>>("Không tìm thấy đơn hàng!");
                if (!IsWaiting(order))
                    return BadRequest<List<AutoSuggestionDto>>($"Đơn {order.OrderCode} không ở trạng thái chờ phân công.");
                var best = (await CandidatesAsync(order, extraLoad))
                    .Where(x => x.CurrentLoad < x.MaxCapacity)
                    .OrderByDescending(x => x.Score).ThenBy(x => x.Priority).ThenBy(x => x.DistanceKm ?? double.MaxValue)
                    .FirstOrDefault();
                if (best != null)
                    extraLoad[best.MerchantId] = extraLoad.GetValueOrDefault(best.MerchantId) + 1;
                result.Add(new AutoSuggestionDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Suggested = best,
                    Message = best == null ? OrderAssignmentRules.NoSuitableMerchant : null
                });
            }
            return Success(result, result.All(x => x.Suggested == null) ? OrderAssignmentRules.NoSuitableMerchant : null);
        }

        // UC 3.3.3 steps 6-9
        public async Task<BaseResponse<List<AssignmentResultDto>>> AutoAssignAsync(AutoAssignCommand command)
        {
            var items = command.Assignments ?? [];
            if (items.Count == 0 || items.Count > MaxBatch || items.Select(x => x.OrderId).Distinct().Count() != items.Count)
                return BadRequest<List<AssignmentResultDto>>($"Vui lòng xác nhận từ 1 đến {MaxBatch} đơn hàng, mỗi đơn một lần.");
            // All-or-nothing: any invalid pair rolls back the whole batch.
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var results = new List<AssignmentResultDto>();
            foreach (var item in items)
            {
                var (result, notFound, error) = await AssignCoreAsync(item.OrderId, item.MerchantId, true);
                if (result == null)
                    return notFound
                        ? NotFound<List<AssignmentResultDto>>("Không tìm thấy đơn hàng!")
                        : BadRequest<List<AssignmentResultDto>>(error!);
                results.Add(result);
            }
            await uow.CompleteAsync();
            return Success(results, "Phân công tự động thành công!");
        }

        private async Task<(AssignmentResultDto? Result, bool NotFound, string? Error)> AssignCoreAsync(
            Guid orderId, Guid merchantId, bool auto)
        {
            // Row lock: two Admins cannot assign the same order at once.
            var order = await _orderRepo.GetForUpdateAsync(orderId);
            if (order == null)
                return (null, true, null);
            if (!IsWaiting(order))
                return (null, false, $"Đơn {order.OrderCode} không ở trạng thái chờ phân công.");
            var candidates = await CandidatesAsync(order);
            if (candidates.Count == 0)
                return (null, false, OrderAssignmentRules.NoMerchantInArea);
            var target = candidates.FirstOrDefault(x => x.MerchantId == merchantId);
            if (target == null)
                return (null, false, $"Merchant được chọn không hoạt động hoặc không phụ trách locker của đơn {order.OrderCode}.");
            if (target.CurrentLoad >= target.MaxCapacity)
                return (null, false, $"Merchant {target.BusinessName} đã đủ tải ({target.CurrentLoad}/{target.MaxCapacity} đơn).");

            order.MerchantId = target.MerchantId;
            order.MerchantName = target.BusinessName;
            order.CommissionRateApplied = target.CommissionRate;
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await _timelineRepo.InsertAsync(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = order.Status,
                Status = order.Status,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? "Admin",
                ActorRole = "Admin",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Admin phân công {(auto ? "tự động " : string.Empty)}đơn cho Merchant {target.BusinessName}."
            }, autoSave: true);
            await _notifications.SendAsync([target.MerchantId], NotificationType.NewOrderForMerchant, "Đơn hàng mới được phân công",
                $"Bạn được phân công đơn #{order.OrderCode} tại {order.LockerName}, ô số {order.BoxNumber}. Vui lòng phân công Shipper lấy đồ.",
                "order", order.Id);
            return (new AssignmentResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                MerchantId = target.MerchantId,
                MerchantName = target.BusinessName,
                CommissionRateApplied = order.CommissionRateApplied
            }, false, null);
        }

        private static bool IsWaiting(WashOrder order) =>
            !order.IsDeleted && order.Status == WashOrderStatus.Deposited && order.MerchantId == null;

        /// <summary>
        /// Merchant đang hoạt động phụ trách locker gửi (và locker trả nếu khác). Điểm = 35 ưu tiên tại locker
        /// + 25 tải lượng còn trống + 25 đánh giá + 15 khoảng cách từ tiệm đến locker.
        /// </summary>
        private async Task<List<MerchantCandidateDto>> CandidatesAsync(WashOrder order, Dictionary<Guid, int>? extraLoad = null)
        {
            var locker = await _lockerRepo.FindAsync(order.LockerId);
            if (locker == null)
                return [];
            var links = await _lockerMerchants.ActiveLinksAsync(locker.Id);
            var returnLockerId = order.ReturnLockerId ?? order.LockerId;
            if (returnLockerId != order.LockerId)
            {
                var returnMerchants = (await _lockerMerchants.ActiveLinksAsync(returnLockerId)).Select(x => x.MerchantId).ToList();
                links = links.Where(x => returnMerchants.Contains(x.MerchantId)).ToList();
            }
            var ids = links.Select(x => x.MerchantId).ToList();
            var merchants = (await _merchantRepo.GetListAsync(m => ids.Contains(m.Id))).ToDictionary(m => m.Id);
            var loads = (await AsyncExecuter.ToListAsync((await _orderRepo.GetQueryableAsync())
                    .Where(o => !o.IsDeleted && o.MerchantId != null && ids.Contains(o.MerchantId.Value) && LoadStatuses.Contains(o.Status))
                    .GroupBy(o => o.MerchantId!.Value)
                    .Select(g => new { MerchantId = g.Key, Count = g.Count() })))
                .ToDictionary(x => x.MerchantId, x => x.Count);

            var result = new List<MerchantCandidateDto>();
            foreach (var link in links)
            {
                if (!merchants.TryGetValue(link.MerchantId, out var m))
                    continue;
                double? distance = m.Latitude == 0 && m.Longitude == 0
                    ? null
                    : DistanceKm((double)m.Latitude, (double)m.Longitude, locker.Latitude, locker.Longitude);
                var load = loads.GetValueOrDefault(m.Id) + (extraLoad?.GetValueOrDefault(m.Id) ?? 0);
                var capacity = Math.Max(1, m.MaxCapacity);
                var priorityScore = 1.0 / Math.Max(1, link.Priority);
                var loadScore = Math.Max(0, 1 - (double)load / capacity);
                var ratingScore = Math.Clamp((double)m.Rating / 5, 0, 1);
                var distanceScore = distance.HasValue ? Math.Max(0, 1 - distance.Value / Math.Max(1, m.ServiceRadiusKm * 2)) : 0;
                result.Add(new MerchantCandidateDto
                {
                    MerchantId = m.Id,
                    BusinessName = m.BusinessName,
                    Rating = m.Rating,
                    CommissionRate = m.CommissionRate,
                    CurrentLoad = load,
                    MaxCapacity = m.MaxCapacity,
                    DistanceKm = distance.HasValue ? Math.Round(distance.Value, 2) : null,
                    Priority = link.Priority,
                    Score = Math.Round(100 * (0.35 * priorityScore + 0.25 * loadScore + 0.25 * ratingScore + 0.15 * distanceScore), 1)
                });
            }
            return result;
        }

        private async Task<List<WaitingOrderDto>> ToWaitingDtosAsync(List<WashOrder> orders, DateTime overdueBefore, DateTime now)
        {
            var lockerIds = orders.Select(o => o.LockerId).Distinct().ToList();
            var orderIds = orders.Select(o => o.Id).ToList();
            var boxIds = orders.Select(o => o.BoxId).Distinct().ToList();
            var lockers = (await _lockerRepo.GetListAsync(l => lockerIds.Contains(l.Id))).ToDictionary(l => l.Id);
            var areaIds = lockers.Values.Where(l => l.ServiceAreaId.HasValue).Select(l => l.ServiceAreaId!.Value).Distinct().ToList();
            var areas = (await _areaRepo.GetListAsync(a => areaIds.Contains(a.Id))).ToDictionary(a => a.Id, a => a.Name);
            var boxes = (await _boxRepo.GetListAsync(b => boxIds.Contains(b.Id))).ToDictionary(b => b.Id, b => b.Size);
            var services = (await _itemRepo.GetListAsync(i => orderIds.Contains(i.WashOrderId)))
                .GroupBy(i => i.WashOrderId).ToDictionary(g => g.Key, g => g.Select(i => i.ServiceName).Distinct().ToList());

            return orders.Select(o =>
            {
                var locker = lockers.GetValueOrDefault(o.LockerId);
                return new WaitingOrderDto
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    LockerId = o.LockerId,
                    LockerName = o.LockerName,
                    BoxNumber = o.BoxNumber,
                    BoxSize = boxes.TryGetValue(o.BoxId, out var size) ? size : null,
                    Services = services.GetValueOrDefault(o.Id) ?? [],
                    DepositedAt = o.DepositedAt,
                    District = locker?.District ?? string.Empty,
                    City = locker?.City ?? string.Empty,
                    ServiceAreaId = locker?.ServiceAreaId,
                    ServiceAreaName = locker?.ServiceAreaId is Guid areaId ? areas.GetValueOrDefault(areaId) : null,
                    IsOverdue = o.MerchantId == null && o.DepositedAt <= overdueBefore,
                    WaitingMinutes = o.DepositedAt.HasValue ? (int)Math.Max(0, (now - o.DepositedAt.Value).TotalMinutes) : 0
                };
            }).ToList();
        }

        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Asin(Math.Sqrt(a));
        }
    }
}
