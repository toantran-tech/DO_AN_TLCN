using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.AdminContracts.Queries;
using WashGo.Application.Contracts.Features.AdminContracts.Results;
using WashGo.Application.Contracts.Features.AdminContracts.Services;
using WashGo.Application.Features.OrderAssignmentApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Core.Utils;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AdminApplication.Services
{
    /// <summary>Admin overview: every number is a SQL aggregate read in one repeatable-read snapshot.</summary>
    public class AdminDashboardAppService : WashGoAppService, IAdminDashboardAppService
    {
        private static readonly WashOrderStatus[] InProgressStatuses =
        [
            WashOrderStatus.Collected, WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Ironing,
            WashOrderStatus.Folding, WashOrderStatus.AwaitingPayment, WashOrderStatus.Paid, WashOrderStatus.ReadyToReturn
        ];

        private readonly IRepository<WashOrder, Guid> _orderRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Customer, Guid> _customerRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<Locker, Guid> _lockerRepo;
        private readonly IRepository<PaymentTransaction, Guid> _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IRepository<OrderRating, Guid> _ratingRepo;
        private readonly SystemSettingProvider _settings;

        public AdminDashboardAppService(IRepository<WashOrder, Guid> orderRepo, IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<User, Guid> userRepo, IRepository<Customer, Guid> customerRepo, IRepository<Merchant, Guid> merchantRepo,
            IRepository<Locker, Guid> lockerRepo, IRepository<PaymentTransaction, Guid> paymentRepo, IRepository<RefundRequest, Guid> refundRepo,
            IRepository<OrderRating, Guid> ratingRepo, SystemSettingProvider settings)
        {
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
            _userRepo = userRepo;
            _customerRepo = customerRepo;
            _merchantRepo = merchantRepo;
            _lockerRepo = lockerRepo;
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _ratingRepo = ratingRepo;
            _settings = settings;
        }

        // ---------------- dashboard ----------------

        public async Task<BaseResponse<AdminDashboardDto>> GetDashboardAsync(AdminDashboardQuery query)
        {
            var (period, fromUtc, toUtc, prevFrom, prevTo, error) =
                DateTimePeriodHelper.ResolvePeriod(query.Period, query.FromDate, query.ToDate, "month");
            if (error != null)
                return BadRequest<AdminDashboardDto>(error);
            var top = Math.Clamp(query.TopMerchants ?? 5, 1, 20);
            var now = DateTime.UtcNow;
            // Upper bounds are inclusive (…23:59:59.999); an open bound means "all history".
            var cur = new Window(fromUtc, toUtc);
            Window? prev = prevFrom.HasValue && prevTo.HasValue ? new Window(prevFrom.Value, prevTo.Value) : null;

            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var orders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted);
            var timeline = await _timelineRepo.GetQueryableAsync();
            var users = await _userRepo.GetQueryableAsync();
            var customers = from c in await _customerRepo.GetQueryableAsync()
                            join u in users on c.Id equals u.Id
                            select u;

            // Customers / Merchants
            var customerTotal = await AsyncExecuter.CountAsync(customers.Where(u => u.IsActive));
            var customerLocked = await AsyncExecuter.CountAsync(customers.Where(u => !u.IsActive));
            async Task<int> NewCustomers(Window w) => await AsyncExecuter.CountAsync(customers.Where(u => (w.From == null || u.CreatedAt >= w.From) && (w.To == null || u.CreatedAt <= w.To)));
            var merchants = await _merchantRepo.GetQueryableAsync();
            var merchantCounts = (await AsyncExecuter.ToListAsync(merchants.GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.Count() })))
                .ToDictionary(x => x.Key, x => x.Count);
            async Task<int> NewMerchants(Window w) => await AsyncExecuter.CountAsync(merchants.Where(m => (w.From == null || m.CreatedAt >= w.From) && (w.To == null || m.CreatedAt <= w.To)));

            // Orders in the period: created (creation time), completed (status history), cancelled (cancel time)
            async Task<int> Created(Window w)
            {
                long a = Unix(w.From, 0), b = Unix(w.To, long.MaxValue);
                return await AsyncExecuter.CountAsync(orders.Where(o => o.CreatedOn >= a && o.CreatedOn <= b));
            }
            async Task<int> Completed(Window w)
            {
                long a = Unix(w.From, 0), b = Unix(w.To, long.MaxValue);
                return await AsyncExecuter.CountAsync((from t in timeline
                                                       join o in orders on t.WashOrderId equals o.Id
                                                       where t.Status == WashOrderStatus.Completed && t.Timestamp >= a && t.Timestamp <= b
                                                       select o.Id).Distinct());
            }
            async Task<int> Cancelled(Window w) =>
                await AsyncExecuter.CountAsync(orders.Where(o => o.Status == WashOrderStatus.Cancelled && o.CancelledAt != null &&
                                                     (w.From == null || o.CancelledAt >= w.From) && (w.To == null || o.CancelledAt <= w.To)));

            var statusCounts = (await AsyncExecuter.ToListAsync(orders.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() })))
                .ToDictionary(x => x.Key, x => x.Count);
            var overdueBefore = now.AddMinutes(-OrderAssignmentRules.AssignmentOverdueMinutes);
            var waiting = orders.Where(o => o.Status == WashOrderStatus.Deposited && o.MerchantId == null);

            // Revenue by transaction time (same formula as /finance/revenue-report)
            async Task<(decimal Laundry, decimal Storage, decimal Cancel, decimal Refunded)> Money(Window w)
            {
                var sums = await AsyncExecuter.ToListAsync((await _paymentRepo.GetQueryableAsync())
                    .Where(p => (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded) &&
                                p.CompletedAt != null && (w.From == null || p.CompletedAt >= w.From) && (w.To == null || p.CompletedAt <= w.To))
                    .GroupBy(p => p.Purpose).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) }));
                var refunded = await AsyncExecuter.SumAsync((await _refundRepo.GetQueryableAsync())
                    .Where(r => r.Status == RefundRequestStatus.Refunded && r.RefundedAt != null &&
                                (w.From == null || r.RefundedAt >= w.From) && (w.To == null || r.RefundedAt <= w.To)),
                    r => (decimal?)r.Amount) ?? 0m;
                decimal Of(PaymentPurpose p) => sums.Where(x => x.Key == p).Sum(x => x.Sum);
                return (Of(PaymentPurpose.Order), Of(PaymentPurpose.OverdueStorageFee), Of(PaymentPurpose.CancellationFee), refunded);
            }

            // Lockers
            var lockers = (await _lockerRepo.GetQueryableAsync()).Where(l => !l.IsDeleted);
            var lockerRows = await AsyncExecuter.ToListAsync(lockers.GroupBy(l => l.Status)
                .Select(g => new { g.Key, Count = g.Count(), Boxes = g.Sum(x => x.TotalBoxes), Free = g.Sum(x => x.AvailableBoxes) }));
            var nearlyFull = await AsyncExecuter.CountAsync(NearlyFull(lockers));

            // Top Merchants in the period
            var topRows = await TopMerchantsAsync(orders, timeline, cur, top);

            var newCustCur = await NewCustomers(cur);
            var newMerCur = await NewMerchants(cur);
            var createdCur = await Created(cur);
            var completedCur = await Completed(cur);
            var cancelledCur = await Cancelled(cur);
            var moneyCur = await Money(cur);
            int? newCustPrev = null, newMerPrev = null, createdPrev = null, completedPrev = null, cancelledPrev = null;
            decimal? revenuePrev = null;
            if (prev is { } p)
            {
                newCustPrev = await NewCustomers(p);
                newMerPrev = await NewMerchants(p);
                createdPrev = await Created(p);
                completedPrev = await Completed(p);
                cancelledPrev = await Cancelled(p);
                var m = await Money(p);
                revenuePrev = m.Laundry + m.Storage + m.Cancel - m.Refunded;
            }
            var waitingCount = await AsyncExecuter.CountAsync(waiting);
            var waitingOverdue = await AsyncExecuter.CountAsync(waiting.Where(o => o.DepositedAt != null && o.DepositedAt < overdueBefore));
            await snapshot.CompleteAsync();

            return Success(new AdminDashboardDto
            {
                GeneratedAt = now,
                Period = period,
                FromDate = Utc(fromUtc),
                ToDate = Utc(toUtc),
                PreviousFromDate = Utc(prevFrom),
                PreviousToDate = Utc(prevTo),
                Customers = new AdminCustomerStatsDto { Total = customerTotal, Locked = customerLocked, New = Kpi(newCustCur, newCustPrev) },
                Merchants = new AdminMerchantStatsDto
                {
                    Active = merchantCounts.GetValueOrDefault(MerchantStatus.Active),
                    Inactive = merchantCounts.GetValueOrDefault(MerchantStatus.Inactive),
                    Suspended = merchantCounts.GetValueOrDefault(MerchantStatus.Suspended),
                    New = Kpi(newMerCur, newMerPrev)
                },
                Orders = new AdminOrderStatsDto
                {
                    Created = Kpi(createdCur, createdPrev),
                    Completed = Kpi(completedCur, completedPrev),
                    Cancelled = Kpi(cancelledCur, cancelledPrev),
                    WaitingAssignment = waitingCount,
                    WaitingAssignmentOverdue = waitingOverdue,
                    InProgress = InProgressStatuses.Sum(s => statusCounts.GetValueOrDefault(s)),
                    CurrentByStatus = statusCounts
                },
                Revenue = new AdminRevenueStatsDto
                {
                    Total = Kpi(moneyCur.Laundry + moneyCur.Storage + moneyCur.Cancel - moneyCur.Refunded, revenuePrev),
                    LaundryRevenue = moneyCur.Laundry,
                    StorageFeeRevenue = moneyCur.Storage,
                    CancellationFeeRevenue = moneyCur.Cancel,
                    RefundedAmount = moneyCur.Refunded
                },
                Lockers = new AdminLockerStatsDto
                {
                    Total = lockerRows.Sum(x => x.Count),
                    Active = lockerRows.Where(x => x.Key == LockerStatus.Active).Sum(x => x.Count),
                    Maintenance = lockerRows.Where(x => x.Key == LockerStatus.Maintenance).Sum(x => x.Count),
                    Inactive = lockerRows.Where(x => x.Key == LockerStatus.Inactive).Sum(x => x.Count),
                    TotalBoxes = lockerRows.Where(x => x.Key == LockerStatus.Active).Sum(x => x.Boxes),
                    AvailableBoxes = lockerRows.Where(x => x.Key == LockerStatus.Active).Sum(x => x.Free),
                    NearlyFull = nearlyFull
                },
                TopMerchants = topRows,
                Definitions = new Dictionary<string, string>
                {
                    ["customers.total"] = "Tài khoản Customer đang hoạt động hiện tại (không tính bị khóa).",
                    ["customers.new"] = "Customer đăng ký trong kỳ.",
                    ["orders.created"] = "Đơn tạo trong kỳ.",
                    ["orders.completed"] = "Đơn chuyển sang Hoàn thành trong kỳ (theo lịch sử trạng thái).",
                    ["orders.cancelled"] = "Đơn bị hủy trong kỳ (theo thời điểm hủy).",
                    ["orders.waitingAssignment"] = "Hiện tại: đơn đã gửi chờ Admin phân công Merchant; quá hạn khi chờ quá 60 phút.",
                    ["revenue.total"] = "Theo thời điểm giao dịch: tiền giặt + phí lưu kho/quá hạn + phí hủy đã thu − tiền đã hoàn. Tổng thu trước chi trả Merchant, không phải lợi nhuận.",
                    ["lockers.nearlyFull"] = "Locker đang hoạt động còn ≤ 10% ô trống.",
                    ["topMerchants"] = "Xếp theo số đơn hoàn thành trong kỳ, rồi tỷ lệ hoàn thành = hoàn thành / (hoàn thành + bị hủy sau khi phân công) trong kỳ; điểm = trung bình sao đánh giá công khai toàn thời gian.",
                    ["changePercent"] = "So với kỳ trước cùng độ dài; null kèm isNew = true khi kỳ trước bằng 0."
                }
            });
        }

        private async Task<List<AdminTopMerchantDto>> TopMerchantsAsync(IQueryable<WashOrder> orders, IQueryable<WashOrderTimeline> timeline,
            Window w, int top)
        {
            long a = Unix(w.From, 0), b = Unix(w.To, long.MaxValue);
            var completed = (await AsyncExecuter.ToListAsync((from t in timeline
                                                              join o in orders on t.WashOrderId equals o.Id
                                                              where o.MerchantId != null && t.Status == WashOrderStatus.Completed &&
                                                                    t.Timestamp >= a && t.Timestamp <= b
                                                              select new { o.Id, MerchantId = o.MerchantId!.Value }).Distinct()
                    .GroupBy(x => x.MerchantId).Select(g => new { g.Key, Count = g.Count() })))
                .ToDictionary(x => x.Key, x => x.Count);
            var cancelled = (await AsyncExecuter.ToListAsync(orders
                    .Where(o => o.MerchantId != null && o.Status == WashOrderStatus.Cancelled && o.CancelledAt != null &&
                                (w.From == null || o.CancelledAt >= w.From) && (w.To == null || o.CancelledAt <= w.To))
                    .GroupBy(o => o.MerchantId!.Value).Select(g => new { g.Key, Count = g.Count() })))
                .ToDictionary(x => x.Key, x => x.Count);
            var ranked = completed.Keys.Union(cancelled.Keys)
                .Select(id =>
                {
                    int c = completed.GetValueOrDefault(id), x = cancelled.GetValueOrDefault(id);
                    return new { Id = id, Completed = c, Cancelled = x, Rate = c + x == 0 ? (decimal?)null : Math.Round(c * 100m / (c + x), 1) };
                })
                .OrderByDescending(x => x.Completed).ThenByDescending(x => x.Rate ?? -1).ThenBy(x => x.Id).Take(top).ToList();
            var ids = ranked.Select(x => x.Id).ToList();
            if (ids.Count == 0)
                return [];
            var names = (await AsyncExecuter.ToListAsync((await _merchantRepo.GetQueryableAsync()).Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.BusinessName, m.Status }))).ToDictionary(x => x.Id);
            var ratings = (await AsyncExecuter.ToListAsync((await _ratingRepo.GetQueryableAsync())
                    .Where(r => !r.IsDeleted && !r.IsHidden && r.MerchantId != null && ids.Contains(r.MerchantId.Value))
                    .GroupBy(r => r.MerchantId!.Value).Select(g => new { g.Key, Avg = g.Average(x => (decimal)x.OverallStars), Count = g.Count() })))
                .ToDictionary(x => x.Key);
            var revenue = (await AsyncExecuter.ToListAsync(
                    (from p in await _paymentRepo.GetQueryableAsync()
                     join o in orders on p.OrderId equals o.Id
                     where o.MerchantId != null && ids.Contains(o.MerchantId.Value) && p.Purpose == PaymentPurpose.Order &&
                           (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded) &&
                           p.CompletedAt != null && (w.From == null || p.CompletedAt >= w.From) && (w.To == null || p.CompletedAt <= w.To)
                     select new { MerchantId = o.MerchantId!.Value, p.Amount })
                    .GroupBy(x => x.MerchantId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })))
                .ToDictionary(x => x.Key, x => x.Sum);
            return ranked.Select(x => new AdminTopMerchantDto
            {
                MerchantId = x.Id,
                BusinessName = names.TryGetValue(x.Id, out var n) ? n.BusinessName : string.Empty,
                Status = names.TryGetValue(x.Id, out var s) ? s.Status : MerchantStatus.Inactive,
                CompletedOrders = x.Completed,
                CancelledOrders = x.Cancelled,
                FinishedOrders = x.Completed + x.Cancelled,
                CompletionRate = x.Rate,
                Rating = ratings.TryGetValue(x.Id, out var r) ? Math.Round(r.Avg, 2) : null,
                RatingCount = ratings.TryGetValue(x.Id, out var rc) ? rc.Count : 0,
                LaundryRevenue = revenue.GetValueOrDefault(x.Id)
            }).ToList();
        }

        // ---------------- alerts ----------------

        private sealed class AlertRow
        {
            public Guid Id { get; init; }
            public AdminAlertType Type { get; init; }
            public DateTime Deadline { get; init; }
        }

        private static AdminAlertSeverity SeverityOf(AdminAlertType type) => type switch
        {
            AdminAlertType.AssignmentOverdue or AdminAlertType.CollectionOverdue or AdminAlertType.ProcessingOverdue or
                AdminAlertType.ReturnOverdue or AdminAlertType.IncidentPendingOverdue or AdminAlertType.ContractExpired => AdminAlertSeverity.High,
            AdminAlertType.PickupOverdue or AdminAlertType.PaymentOverdue or AdminAlertType.LockerNearlyFull or AdminAlertType.ContractExpiring => AdminAlertSeverity.Medium,
            _ => AdminAlertSeverity.Low
        };

        private static string TitleOf(AdminAlertType type) => type switch
        {
            AdminAlertType.AssignmentOverdue => "Đơn chờ phân công Merchant quá 60 phút",
            AdminAlertType.CollectionOverdue => "Merchant trễ hạn lấy đồ",
            AdminAlertType.ProcessingOverdue => "Đơn quá thời gian giặt sấy dự kiến",
            AdminAlertType.ReturnOverdue => "Merchant trễ hạn trả đồ",
            AdminAlertType.PickupOverdue => "Khách quá hạn lấy đồ",
            AdminAlertType.PaymentOverdue => "Khách quá hạn thanh toán",
            AdminAlertType.DepositExpired => "Đơn quá hạn gửi đồ",
            AdminAlertType.LockerNearlyFull => "Locker sắp hết ô trống",
            _ => type.ToString()
        };

        private sealed class LockerPick
        {
            public Guid Id { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int AvailableBoxes { get; init; }
            public int TotalBoxes { get; init; }
        }

        private static IQueryable<Locker> NearlyFull(IQueryable<Locker> lockers) =>
            lockers.Where(l => l.Status == LockerStatus.Active && l.TotalBoxes > 0 && l.AvailableBoxes * 10 <= l.TotalBoxes);

        public async Task<BaseResponse<AdminAlertsDto>> GetAlertsAsync(AdminAlertsQuery query)
        {
            var page = Math.Max(1, query.Page ?? 1);
            var take = Math.Clamp(query.Take ?? 20, 1, 100);
            var settings = await _settings.GetAsync();
            int collectSla = settings.MerchantCollectSlaHours > 0 ? settings.MerchantCollectSlaHours : 4;
            int returnSla = settings.MerchantReturnSlaHours > 0 ? settings.MerchantReturnSlaHours : 24;
            int assignMinutes = OrderAssignmentRules.AssignmentOverdueMinutes;
            var now = DateTime.UtcNow;
            var collectBefore = now.AddHours(-collectSla);
            var returnBefore = now.AddHours(-returnSla);
            var assignBefore = now.AddMinutes(-assignMinutes);

            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var orders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted);
            var sources = new Dictionary<AdminAlertType, IQueryable<AlertRow>>
            {
                [AdminAlertType.AssignmentOverdue] = orders
                    .Where(o => o.Status == WashOrderStatus.Deposited && o.MerchantId == null && o.DepositedAt != null && o.DepositedAt < assignBefore)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.AssignmentOverdue, Deadline = o.DepositedAt!.Value.AddMinutes(assignMinutes) }),
                [AdminAlertType.CollectionOverdue] = orders
                    .Where(o => o.Status == WashOrderStatus.Deposited && o.MerchantId != null && o.DepositedAt != null && o.DepositedAt.Value.AddHours(o.CollectSlaHoursSnapshot ?? collectSla) < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.CollectionOverdue, Deadline = o.DepositedAt!.Value.AddHours(o.CollectSlaHoursSnapshot ?? collectSla) }),
                [AdminAlertType.ProcessingOverdue] = orders
                    .Where(o => (o.Status == WashOrderStatus.Washing || o.Status == WashOrderStatus.Drying ||
                                 o.Status == WashOrderStatus.Ironing || o.Status == WashOrderStatus.Folding) &&
                                o.EstimatedFinishAt != null && o.EstimatedFinishAt < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.ProcessingOverdue, Deadline = o.EstimatedFinishAt!.Value }),
                [AdminAlertType.ReturnOverdue] = orders
                    .Where(o => o.Status == WashOrderStatus.Paid && o.PaidAt != null && o.PaidAt.Value.AddHours(o.ReturnSlaHoursSnapshot ?? returnSla) < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.ReturnOverdue, Deadline = o.PaidAt!.Value.AddHours(o.ReturnSlaHoursSnapshot ?? returnSla) }),
                [AdminAlertType.PickupOverdue] = orders
                    .Where(o => o.Status == WashOrderStatus.ReadyToReturn && o.PickupDeadline != null && o.PickupDeadline < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.PickupOverdue, Deadline = o.PickupDeadline!.Value }),
                [AdminAlertType.PaymentOverdue] = orders
                    .Where(o => o.Status == WashOrderStatus.AwaitingPayment && o.PaymentStatus == PaymentStatus.Unpaid &&
                                o.PaymentDeadline != null && o.PaymentDeadline < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.PaymentOverdue, Deadline = o.PaymentDeadline!.Value }),
                [AdminAlertType.DepositExpired] = orders
                    .Where(o => o.Status == WashOrderStatus.Pending && o.DepositDeadline != null && o.DepositDeadline < now)
                    .Select(o => new AlertRow { Id = o.Id, Type = AdminAlertType.DepositExpired, Deadline = o.DepositDeadline!.Value })
            };
            var incidents = (await LazyServiceProvider.LazyGetRequiredService<IRepository<Incident, Guid>>().GetQueryableAsync()).Where(x => !x.IsDeleted);
            var incidentCutoff = new DateTimeOffset(now.AddHours(-48)).ToUnixTimeSeconds();
            sources[AdminAlertType.IncidentPendingOverdue] = incidents.Where(x => x.Status == IncidentStatus.Pending && x.CreatedOn < incidentCutoff)
                .Select(x => new AlertRow { Id = x.Id, Type = AdminAlertType.IncidentPendingOverdue, Deadline = DateTime.UnixEpoch.AddSeconds(x.CreatedOn).AddHours(48) });
            var merchants = await _merchantRepo.GetQueryableAsync();
            sources[AdminAlertType.ContractExpiring] = merchants.Where(x => x.ContractEndDate != null && x.ContractEndDate.Value.AddHours(7).Date.AddDays(1).AddHours(-7) > now && x.ContractEndDate.Value.AddHours(7).Date.AddDays(1).AddHours(-7) <= now.AddDays(30))
                .Select(x => new AlertRow { Id = x.Id, Type = AdminAlertType.ContractExpiring, Deadline = x.ContractEndDate!.Value.AddHours(7).Date.AddDays(1).AddHours(-7) });
            sources[AdminAlertType.ContractExpired] = merchants.Where(x => x.ContractEndDate != null && x.ContractEndDate.Value.AddHours(7).Date.AddDays(1).AddHours(-7) <= now)
                .Select(x => new AlertRow { Id = x.Id, Type = AdminAlertType.ContractExpired, Deadline = x.ContractEndDate!.Value.AddHours(7).Date.AddDays(1).AddHours(-7) });
            var counts = new Dictionary<AdminAlertType, int>();
            foreach (var (type, source) in sources)
                counts[type] = await AsyncExecuter.CountAsync(source);
            var lockers = (await _lockerRepo.GetQueryableAsync()).Where(l => !l.IsDeleted);
            // Counted and paged in the database; locker alerts share deadline "now", so they are ordered by Id like the final sort.
            var fullLockers = NearlyFull(lockers).OrderBy(l => l.Id);
            counts[AdminAlertType.LockerNearlyFull] = await AsyncExecuter.CountAsync(fullLockers);

            bool Wanted(AdminAlertType t) => (query.Type == null || query.Type == t) && (query.Severity == null || query.Severity == SeverityOf(t));
            var total = counts.Where(x => Wanted(x.Key)).Sum(x => x.Value);

            // Ordered by severity, then (inside a severity) order alerts by deadline before locker alerts (checked "now").
            var rows = new List<AlertRow>();
            var lockerPicks = new List<LockerPick>();
            int offset = (page - 1) * take, need = take;
            foreach (var severity in new[] { AdminAlertSeverity.High, AdminAlertSeverity.Medium, AdminAlertSeverity.Low })
            {
                if (need == 0) break;
                var types = sources.Keys.Where(t => SeverityOf(t) == severity && Wanted(t)).ToList();
                var orderCount = types.Sum(t => counts[t]);
                var lockerCount = SeverityOf(AdminAlertType.LockerNearlyFull) == severity && Wanted(AdminAlertType.LockerNearlyFull)
                    ? counts[AdminAlertType.LockerNearlyFull] : 0;
                if (offset >= orderCount + lockerCount) { offset -= orderCount + lockerCount; continue; }
                if (offset < orderCount && types.Count > 0)
                {
                    var union = types.Select(t => sources[t]).Aggregate((x, y) => x.Concat(y));
                    var got = await AsyncExecuter.ToListAsync(union.OrderBy(x => x.Deadline).ThenBy(x => x.Id).ThenBy(x => x.Type).Skip(offset).Take(need));
                    rows.AddRange(got);
                    need -= got.Count;
                    offset = 0;
                }
                else
                    offset -= orderCount;
                if (need > 0 && lockerCount > 0)
                {
                    var got = await AsyncExecuter.ToListAsync(fullLockers.Skip(offset).Take(need)
                        .Select(l => new LockerPick { Id = l.Id, Code = l.Code, Name = l.Name, AvailableBoxes = l.AvailableBoxes, TotalBoxes = l.TotalBoxes }));
                    lockerPicks.AddRange(got);
                    need -= got.Count;
                    offset = 0;
                }
            }
            var ids = rows.Select(x => x.Id).ToList();
            var details = (await AsyncExecuter.ToListAsync(orders.Where(o => ids.Contains(o.Id))
                    .Select(o => new { o.Id, o.OrderCode, o.MerchantId, o.MerchantName, o.LockerName })))
                .ToDictionary(x => x.Id);
            var incidentIds = rows.Where(x => x.Type == AdminAlertType.IncidentPendingOverdue).Select(x => x.Id).ToList();
            var incidentDetails = (await AsyncExecuter.ToListAsync(incidents.Where(x => incidentIds.Contains(x.Id)))).ToDictionary(x => x.Id);
            var merchantIds = rows.Where(x => x.Type == AdminAlertType.ContractExpiring || x.Type == AdminAlertType.ContractExpired).Select(x => x.Id).ToList();
            var merchantDetails = (await AsyncExecuter.ToListAsync(merchants.Where(x => merchantIds.Contains(x.Id)))).ToDictionary(x => x.Id);
            await snapshot.CompleteAsync();

            var items = rows.Where(x => details.ContainsKey(x.Id)).Select(x =>
            {
                var o = details[x.Id];
                var deadline = DateTime.SpecifyKind(x.Deadline, DateTimeKind.Utc);
                return new AdminAlertDto
                {
                    Type = x.Type, Severity = SeverityOf(x.Type), Title = TitleOf(x.Type), ResourceType = "Order", ResourceId = o.Id,
                    ResourceCode = o.OrderCode, MerchantId = o.MerchantId, MerchantName = o.MerchantName, LockerName = o.LockerName,
                    Deadline = deadline, OverdueMinutes = Math.Max(0, (int)(now - deadline).TotalMinutes)
                };
            }).ToList();
            items.AddRange(rows.Where(x => incidentDetails.ContainsKey(x.Id) && x.Type == AdminAlertType.IncidentPendingOverdue).Select(x => new AdminAlertDto
            {
                Type = x.Type, Severity = SeverityOf(x.Type), Title = "Khiếu nại chờ xử lý quá 48 giờ", ResourceType = "Incident", ResourceId = x.Id,
                ResourceCode = incidentDetails[x.Id].OrderCode, MerchantId = incidentDetails[x.Id].MerchantId, MerchantName = incidentDetails[x.Id].MerchantName,
                Deadline = DateTime.SpecifyKind(x.Deadline, DateTimeKind.Utc), OverdueMinutes = Math.Max(0, (int)(now - x.Deadline).TotalMinutes)
            }));
            items.AddRange(rows.Where(x => merchantDetails.ContainsKey(x.Id) && (x.Type == AdminAlertType.ContractExpiring || x.Type == AdminAlertType.ContractExpired)).Select(x => new AdminAlertDto
            {
                Type = x.Type, Severity = SeverityOf(x.Type), Title = x.Type == AdminAlertType.ContractExpired ? "Hợp đồng Merchant đã hết hạn" : "Hợp đồng Merchant sắp hết hạn trong 30 ngày",
                ResourceType = "Merchant", ResourceId = x.Id, ResourceCode = merchantDetails[x.Id].BusinessName, MerchantId = x.Id, MerchantName = merchantDetails[x.Id].BusinessName,
                Deadline = DateTime.SpecifyKind(x.Deadline, DateTimeKind.Utc), OverdueMinutes = Math.Max(0, (int)(now - x.Deadline).TotalMinutes)
            }));
            items.AddRange(lockerPicks.Select(l =>
            {
                return new AdminAlertDto
                {
                    Type = AdminAlertType.LockerNearlyFull, Severity = SeverityOf(AdminAlertType.LockerNearlyFull),
                    Title = l.AvailableBoxes == 0 ? "Locker đã hết ô trống" : TitleOf(AdminAlertType.LockerNearlyFull),
                    ResourceType = "Locker", ResourceId = l.Id, ResourceCode = l.Code, LockerName = l.Name,
                    Deadline = now, OverdueMinutes = 0, AvailableBoxes = l.AvailableBoxes, TotalBoxes = l.TotalBoxes
                };
            }));
            // Re-apply the global order (severity, then the order within each severity group as built above).
            items = items.OrderByDescending(x => x.Severity).ThenBy(x => x.Deadline).ThenBy(x => x.ResourceId).ThenBy(x => x.Type).ToList();

            return Success(new AdminAlertsDto
            {
                GeneratedAt = now,
                Page = page,
                Take = take,
                Total = total,
                TotalAll = counts.Values.Sum(),
                Summary = counts.Select(x => new AdminAlertCountDto { Type = x.Key, Severity = SeverityOf(x.Key), Count = x.Value })
                    .OrderBy(x => x.Type).ToList(),
                Items = items
            });
        }

        // ---------------- lookups ----------------

        public async Task<PaginatedResult<AdminLookupItemDto>> LookupMerchantsAsync(AdminLookupQuery query)
        {
            var q = await _merchantRepo.GetQueryableAsync();
            if (query.Status.HasValue) q = q.Where(m => (int)m.Status == query.Status.Value);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim().ToLower();
                q = q.Where(m => m.BusinessName.ToLower().Contains(k));
            }
            return await PageAsync(q.OrderBy(m => m.BusinessName).ThenBy(m => m.Id)
                .Select(m => new AdminLookupItemDto { Id = m.Id, Name = m.BusinessName, Status = (int)m.Status }), query);
        }

        public async Task<PaginatedResult<AdminLookupItemDto>> LookupLockersAsync(AdminLookupQuery query)
        {
            var q = (await _lockerRepo.GetQueryableAsync()).Where(l => !l.IsDeleted);
            if (query.Status.HasValue) q = q.Where(l => (int)l.Status == query.Status.Value);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim().ToLower();
                q = q.Where(l => l.Name.ToLower().Contains(k) || l.Code.ToLower().Contains(k));
            }
            return await PageAsync(q.OrderBy(l => l.Name).ThenBy(l => l.Id).Select(l => new AdminLookupItemDto
            {
                Id = l.Id, Name = l.Name, Code = l.Code, Status = (int)l.Status, AvailableBoxes = l.AvailableBoxes, TotalBoxes = l.TotalBoxes
            }), query);
        }

        private async Task<PaginatedResult<AdminLookupItemDto>> PageAsync(IQueryable<AdminLookupItemDto> q, AdminLookupQuery query)
        {
            var page = Math.Max(1, query.Page ?? 1);
            var take = Math.Clamp(query.Take ?? 20, 1, 50);
            var total = await AsyncExecuter.LongCountAsync(q);
            var items = await AsyncExecuter.ToListAsync(q.Skip((page - 1) * take).Take(take));
            return Success(items, total, page, take);
        }

        // ---------------- helpers ----------------

        /// <summary>Inclusive bounds; null = open (period "all").</summary>
        private readonly record struct Window(DateTime? From, DateTime? To);

        private static long Unix(DateTime? utc, long open) =>
            utc is { } v ? new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)).ToUnixTimeSeconds() : open;

        private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

        private static AdminKpiDto Kpi(decimal current, decimal? previous) => new()
        {
            Current = current,
            Previous = previous,
            ChangePercent = previous is not { } p ? null : p == 0 ? (current == 0 ? 0 : null) : Math.Round((current - p) / Math.Abs(p) * 100m, 1),
            IsNew = previous == 0 && current != 0
        };
    }
}
