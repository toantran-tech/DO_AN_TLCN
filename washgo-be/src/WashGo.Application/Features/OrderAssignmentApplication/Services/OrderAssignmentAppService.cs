using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
using WashGo.Domain.Entities.Feedback;
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
        private readonly IRepository<OrderOperation, Guid> _operationRepo;
        private readonly IRepository<OrderRating, Guid> _ratingRepo;

        public OrderAssignmentAppService(
            IWashOrderRepository orderRepo,
            IRepository<WashOrderItem, Guid> itemRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            ILockerRepository lockerRepo,
            IRepository<LockerBox, Guid> boxRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<ServiceArea, Guid> areaRepo,
            LockerMerchantResolver lockerMerchants,
            NotificationPublisher notifications,
            IRepository<OrderOperation, Guid> operationRepo,
            IRepository<OrderRating, Guid> ratingRepo)
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
            _operationRepo = operationRepo;
            _ratingRepo = ratingRepo;
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
                Merchants = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Priority).ThenBy(x => x.MerchantId).ToList()
            }, candidates.Count == 0 ? OrderAssignmentRules.NoMerchantInArea : null);
        }

        // UC 3.3.2 steps 5-10
        private const string AssignOperation = "AssignMerchant";
        private const string AutoAssignOperation = "AutoAssignMerchant";

        private sealed record Failure(HttpStatusCode Status, string Code, string Message);

        private BaseResponse<T> Fail<T>(Failure f) => new()
        {
            Succeeded = false, StatusCode = f.Status, Message = f.Message, Meta = new { code = f.Code }
        };

        public async Task<BaseResponse<AssignmentResultDto>> AssignAsync(Guid orderId, AssignOrderCommand command)
        {
            if (command.OperationId == Guid.Empty)
                return BadRequest<AssignmentResultDto>("operationId không hợp lệ.");
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var pair = new List<(Guid OrderId, Guid MerchantId)> { (orderId, command.MerchantId) };
            if (command.OperationId is { } operationId)
            {
                var (replay, conflict) = await ReplayAsync(operationId, AssignOperation, pair);
                if (conflict != null)
                    return Fail<AssignmentResultDto>(conflict);
                if (replay != null)
                {
                    await uow.CompleteAsync();
                    return Replayed(replay[0]);
                }
            }
            // Capacity lock first (same order as the batch: Merchant, then order) so concurrent assignments cannot overfill.
            await _orderRepo.AcquireLockAsync(CapacityLock(command.MerchantId));
            var (result, failure) = await AssignCoreAsync(orderId, command.MerchantId, false, command.ExpectedRevision, command.ExpectedCommissionRate);
            if (result == null)
                return Fail<AssignmentResultDto>(failure!);
            if (command.OperationId is { } op)
                await RecordAsync(op, op, AssignOperation, result);
            await AuditAssignAsync(result, "AssignMerchant");
            await uow.CompleteAsync();
            return Success(result, "Phân công thành công!");
        }

        private BaseResponse<T> Replayed<T>(T data) => new()
        {
            Succeeded = true, StatusCode = HttpStatusCode.OK, Data = data,
            Message = "Thao tác phân công này đã được xử lý trước đó; không thực hiện lại.",
            Meta = new { code = "Replayed", replayed = true }
        };

        private static string CapacityLock(Guid merchantId) => $"merchant-capacity:{merchantId}";

        private static Guid ItemId(Guid operationId, Guid orderId) =>
            new(MD5.HashData(Encoding.UTF8.GetBytes($"{operationId}:{orderId}")));

        /// <summary>Same key, same actor, same operation and same order–Merchant pairs → stored results; otherwise 409.</summary>
        private async Task<(List<AssignmentResultDto>? Results, Failure? Conflict)> ReplayAsync(
            Guid operationId, string operation, List<(Guid OrderId, Guid MerchantId)> pairs)
        {
            var rows = await _operationRepo.GetListAsync(x => x.GroupId == operationId || x.Id == operationId);
            if (rows.Count == 0)
                return (null, null);
            var requested = pairs.Select(x => $"{x.OrderId}|merchantId={x.MerchantId}").OrderBy(x => x).ToList();
            var stored = rows.Select(x => $"{x.OrderId}|{x.Request}").OrderBy(x => x).ToList();
            if (rows.Any(x => x.Operation != operation || x.ActorId != CurrentUser.Id) || !requested.SequenceEqual(stored))
                return (null, new Failure(HttpStatusCode.Conflict, "OperationIdReused", "operationId này đã được dùng cho một thao tác khác."));
            return (await ResultsAsync(rows), null);
        }

        private async Task<List<AssignmentResultDto>> ResultsAsync(List<OrderOperation> rows)
        {
            var ids = rows.Select(x => x.OrderId).ToList();
            var orders = (await _orderRepo.GetListAsync(o => ids.Contains(o.Id))).ToDictionary(o => o.Id);
            return rows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Where(x => orders.ContainsKey(x.OrderId)).Select(x =>
            {
                var o = orders[x.OrderId];
                return new AssignmentResultDto
                {
                    OrderId = o.Id, OrderCode = o.OrderCode, MerchantId = o.MerchantId ?? Guid.Empty,
                    MerchantName = o.MerchantName ?? string.Empty, CommissionRateApplied = o.CommissionRateApplied,
                    ResultRevision = x.ResultRevision
                };
            }).ToList();
        }

        private Task AuditAssignAsync(AssignmentResultDto result, string action) =>
            Audit.LogAsync(new AuditEntry("Order", action, "Order", result.OrderId.ToString(), result.OrderCode)
            {
                ChangedFields = ["merchantId", "commissionRateApplied"],
                Details = new { result.MerchantId, result.MerchantName, result.CommissionRateApplied, result.ResultRevision }
            });

        private async Task RecordAsync(Guid id, Guid groupId, string operation, AssignmentResultDto result)
        {
            await _operationRepo.InsertAsync(new OrderOperation(id)
            {
                GroupId = groupId, OrderId = result.OrderId, ActorId = CurrentUser.Id ?? Guid.Empty, Operation = operation,
                Request = $"merchantId={result.MerchantId}", ResultStatus = WashOrderStatus.Deposited,
                ResultRevision = result.ResultRevision, CreatedAt = DateTime.UtcNow
            }, autoSave: true);
        }

        public async Task<BaseResponse<AssignmentOperationDto>> GetOperationAsync(Guid operationId)
        {
            var rows = await _operationRepo.GetListAsync(x => (x.GroupId == operationId || x.Id == operationId) &&
                                                             (x.Operation == AssignOperation || x.Operation == AutoAssignOperation));
            if (rows.Count == 0)
                return NotFound<AssignmentOperationDto>("Không tìm thấy thao tác này: yêu cầu chưa tới server hoặc đã thất bại, có thể gửi lại cùng operationId.");
            return Success(new AssignmentOperationDto
            {
                OperationId = operationId, Operation = rows[0].Operation, CreatedAt = rows.Min(x => x.CreatedAt),
                Results = await ResultsAsync(rows)
            });
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
                    .OrderByDescending(x => x.Score).ThenBy(x => x.Priority).ThenBy(x => x.DistanceKm ?? double.MaxValue).ThenBy(x => x.MerchantId)
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
            if (command.OperationId == Guid.Empty)
                return BadRequest<List<AssignmentResultDto>>("operationId không hợp lệ.");
            // All-or-nothing: any invalid pair rolls back the whole batch.
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (command.OperationId is { } operationId)
            {
                var (replay, conflict) = await ReplayAsync(operationId, AutoAssignOperation,
                    items.Select(x => (x.OrderId, x.MerchantId)).ToList());
                if (conflict != null)
                    return Fail<List<AssignmentResultDto>>(conflict);
                if (replay != null)
                {
                    await uow.CompleteAsync();
                    return Replayed(replay);
                }
            }
            // Lock every Merchant's capacity in a stable order (by id) before touching orders: no deadlock, no overfill.
            foreach (var merchantId in items.Select(x => x.MerchantId).Distinct().OrderBy(x => x))
                await _orderRepo.AcquireLockAsync(CapacityLock(merchantId));
            var results = new List<AssignmentResultDto>();
            foreach (var item in items)
            {
                var (result, failure) = await AssignCoreAsync(item.OrderId, item.MerchantId, true, item.ExpectedRevision, item.ExpectedCommissionRate);
                if (result == null)
                    return Fail<List<AssignmentResultDto>>(failure!);
                results.Add(result);
            }
            if (command.OperationId is { } op)
                foreach (var result in results)
                    await RecordAsync(ItemId(op, result.OrderId), op, AutoAssignOperation, result);
            foreach (var result in results)
                await AuditAssignAsync(result, "AutoAssignMerchant");
            await uow.CompleteAsync();
            return Success(results, "Phân công tự động thành công!");
        }

        private async Task<(AssignmentResultDto? Result, Failure? Failure)> AssignCoreAsync(
            Guid orderId, Guid merchantId, bool auto, long? expectedRevision, decimal? expectedCommissionRate)
        {
            // Row lock: two Admins cannot assign the same order at once. The Merchant's capacity lock is already held,
            // so load, Active status and locker links below are read at commit time.
            var order = await _orderRepo.GetForUpdateAsync(orderId);
            if (order == null)
                return (null, new Failure(HttpStatusCode.NotFound, "NotFound", "Không tìm thấy đơn hàng!"));
            if (expectedRevision.HasValue && expectedRevision.Value != order.Revision)
                return (null, new Failure(HttpStatusCode.Conflict, "RevisionMismatch",
                    $"Đơn {order.OrderCode} đã thay đổi kể từ lúc bạn xem; vui lòng tải lại trước khi phân công."));
            if (!IsWaiting(order))
                return (null, new Failure(HttpStatusCode.BadRequest, "NotWaiting", $"Đơn {order.OrderCode} không ở trạng thái chờ phân công."));
            var merchant = await _merchantRepo.FindAsync(merchantId);
            if (merchant != null && MerchantContractRules.IsExpired(merchant.ContractEndDate, DateTime.UtcNow))
                return (null, new Failure(HttpStatusCode.BadRequest, "ContractExpired", "Hợp đồng Merchant đã hết hạn; không thể phân công đơn mới. Các đơn đã nhận vẫn được xử lý."));
            var candidates = await CandidatesAsync(order);
            if (candidates.Count == 0)
                return (null, new Failure(HttpStatusCode.BadRequest, "NoMerchantInArea", OrderAssignmentRules.NoMerchantInArea));
            var target = candidates.FirstOrDefault(x => x.MerchantId == merchantId);
            if (target == null)
                return (null, new Failure(HttpStatusCode.BadRequest, "MerchantNotEligible",
                    $"Merchant được chọn không hoạt động hoặc không phụ trách locker của đơn {order.OrderCode}."));
            if (expectedCommissionRate.HasValue && expectedCommissionRate.Value != target.CommissionRate)
                return (null, new Failure(HttpStatusCode.Conflict, "CommissionChanged",
                    $"Tỷ lệ hoa hồng của {target.BusinessName} đã đổi ({expectedCommissionRate:0.##}% → {target.CommissionRate:0.##}%); vui lòng xem lại."));
            if (target.CurrentLoad >= target.MaxCapacity)
                return (null, new Failure(HttpStatusCode.BadRequest, "CapacityFull",
                    $"Merchant {target.BusinessName} đã đủ tải ({target.CurrentLoad}/{target.MaxCapacity} đơn)."));

            order.MerchantId = target.MerchantId;
            order.MerchantName = target.BusinessName;
            order.CommissionRateApplied = target.CommissionRate;
            order.CommissionLockedAt = DateTime.UtcNow;
            order.CommissionSnapshotSource = "Assignment";
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
                CommissionRateApplied = order.CommissionRateApplied,
                ResultRevision = order.Revision
            }, null);
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
            var ratingCounts = (await AsyncExecuter.ToListAsync((await _ratingRepo.GetQueryableAsync())
                    .Where(r => !r.IsDeleted && !r.IsHidden && r.MerchantId != null && ids.Contains(r.MerchantId.Value))
                    .GroupBy(r => r.MerchantId!.Value).Select(g => new { MerchantId = g.Key, Count = g.Count() })))
                .ToDictionary(x => x.MerchantId, x => x.Count);

            var result = new List<MerchantCandidateDto>();
            foreach (var link in links)
            {
                if (!merchants.TryGetValue(link.MerchantId, out var m) || m.Status != MerchantStatus.Active || MerchantContractRules.IsExpired(m.ContractEndDate, DateTime.UtcNow))
                    continue;
                double? distance = m.Latitude == 0 && m.Longitude == 0
                    ? null
                    : DistanceKm((double)m.Latitude, (double)m.Longitude, locker.Latitude, locker.Longitude);
                var load = loads.GetValueOrDefault(m.Id) + (extraLoad?.GetValueOrDefault(m.Id) ?? 0);
                var capacity = Math.Max(1, m.MaxCapacity);
                var priorityScore = 1.0 / Math.Max(1, link.Priority);
                var loadScore = Math.Max(0, 1 - (double)load / capacity);
                var effectiveRating = ratingCounts.GetValueOrDefault(m.Id) == 0 ? 3m : m.Rating;
                var ratingScore = Math.Clamp((double)effectiveRating / 5, 0, 1);
                var distanceScore = distance.HasValue ? Math.Max(0, 1 - distance.Value / Math.Max(1, m.ServiceRadiusKm * 2)) : 0;
                result.Add(new MerchantCandidateDto
                {
                    MerchantId = m.Id,
                    BusinessName = m.BusinessName,
                    Rating = ratingCounts.GetValueOrDefault(m.Id) == 0 ? null : m.Rating, RatingForScore = effectiveRating,
                    PriorityScore = Math.Round(35 * priorityScore, 4), LoadScore = Math.Round(25 * loadScore, 4),
                    RatingScore = Math.Round(25 * ratingScore, 4), DistanceScore = Math.Round(15 * distanceScore, 4),
                    PublicRatingCount = ratingCounts.GetValueOrDefault(m.Id),
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
            var itemRows = await _itemRepo.GetListAsync(i => orderIds.Contains(i.WashOrderId));
            var services = itemRows.GroupBy(i => i.WashOrderId).ToDictionary(g => g.Key, g => g.Select(i => i.ServiceName).Distinct().ToList());
            // Batched: one query for the service images of the whole page (deleted services included).
            var serviceIds = itemRows.Select(i => i.ServiceId).Distinct().ToList();
            var serviceImages = (await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<WashGo.Domain.Entities.WashService.WashService, Guid>>()
                    .GetQueryableAsync()).Where(s => serviceIds.Contains(s.Id)).Select(s => new { s.Id, s.Images })))
                .ToDictionary(x => x.Id, x => WashGo.Application.Features.FileApplication.Services.StoredImageUrls.Read(x.Images));

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
                    ServiceItems = itemRows.Where(i => i.WashOrderId == o.Id).GroupBy(i => i.ServiceId)
                        .Select(g => new WashGo.Application.Contracts.Features.WashOrderContracts.Results.OrderServiceRefDto
                        {
                            ServiceId = g.Key, ServiceName = g.First().ServiceName,
                            ServiceThumbnailUrl = serviceImages.GetValueOrDefault(g.Key)?.FirstOrDefault()
                        }).ToList(),
                    LockerImageUrls = locker == null ? [] : WashGo.Application.Features.FileApplication.Services.StoredImageUrls.Read(locker.Images),
                    LockerThumbnailUrl = locker == null ? null : WashGo.Application.Features.FileApplication.Services.StoredImageUrls.Read(locker.Images).FirstOrDefault(),
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
