using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Results;
using WashGo.Application.Contracts.Features.MerchantReportContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.MerchantReportApplication.Services
{
    /// <summary>
    /// Báo cáo kinh doanh của Merchant đang đăng nhập (giờ Việt Nam UTC+7). Mọi số liệu đọc trong một transaction
    /// repeatable-read nên tổng, biểu đồ, dịch vụ bán chạy và file xuất cùng một snapshot.
    /// </summary>
    public class MerchantReportAppService : WashGoAppService, IMerchantReportAppService
    {
        private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
        private const int MaxBuckets = 400;
        private const int MaxDays = 366;

        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<PaymentTransaction, Guid> _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IWashServiceRepository _serviceRepo;

        public MerchantReportAppService(
            IWashOrderRepository orderRepo,
            IRepository<PaymentTransaction, Guid> paymentRepo,
            IRepository<RefundRequest, Guid> refundRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<WashOrderItem, Guid> itemRepo,
            IWashServiceRepository serviceRepo)
        {
            _orderRepo = orderRepo;
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _timelineRepo = timelineRepo;
            _itemRepo = itemRepo;
            _serviceRepo = serviceRepo;
        }

        private sealed record Range(DateTime From, DateTime To);
        private sealed record Plan(string Period, string Granularity, DateTime Start, DateTime End, DateTime AsOf,
            TimeSpan Length, List<(DateTime Start, DateTime End, string Label)> Buckets);

        private BaseResponse<T> Invalid<T>(string code, string message) => new()
        {
            Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = message, Meta = new { code }
        };

        public async Task<BaseResponse<MerchantReportDto>> GetOverviewAsync(MerchantReportQuery query)
        {
            var (plan, error, code) = BuildPlan(query, DateTime.UtcNow);
            if (plan == null)
                return Invalid<MerchantReportDto>(code!, error!);
            return Success(await BuildAsync(plan, Math.Clamp(query.Top ?? 5, 1, 20)));
        }

        public async Task<BaseResponse<ReportFileDto>> ExportCsvAsync(MerchantReportQuery query)
        {
            var (plan, error, code) = BuildPlan(query, DateTime.UtcNow);
            if (plan == null)
                return Invalid<ReportFileDto>(code!, error!);
            var report = await BuildAsync(plan, Math.Clamp(query.Top ?? 20, 1, 20));
            return Success(new ReportFileDto
            {
                FileName = $"bao-cao-{report.Current.FromLocalDate}-{report.Current.ToLocalDate}.csv",
                Content = ToCsv(report)
            });
        }

        // ---------- period and buckets ----------

        private static (Plan? Plan, string? Error, string? Code) BuildPlan(MerchantReportQuery query, DateTime nowUtc)
        {
            var nowLocal = nowUtc + Vn;
            var period = (query.Period ?? "month").Trim().ToLowerInvariant();
            DateTime startLocal, endLocal;
            switch (period)
            {
                case "today":
                    startLocal = nowLocal.Date; endLocal = startLocal.AddDays(1); break;
                case "week":
                    startLocal = nowLocal.Date.AddDays(-(((int)nowLocal.DayOfWeek + 6) % 7)); endLocal = startLocal.AddDays(7); break;
                case "month":
                    startLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1); endLocal = startLocal.AddMonths(1); break;
                case "custom":
                    if (!DateTime.TryParseExact(query.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
                        !DateTime.TryParseExact(query.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                        return (null, "Kỳ tùy chọn cần from và to dạng yyyy-MM-dd.", "InvalidRange");
                    if (from > to)
                        return (null, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.", "InvalidRange");
                    if ((to - from).TotalDays + 1 > MaxDays)
                        return (null, $"Khoảng báo cáo tối đa {MaxDays} ngày.", "InvalidRange");
                    startLocal = from; endLocal = to.AddDays(1); break;
                default:
                    return (null, "period phải là today, week, month hoặc custom.", "InvalidPeriod");
            }
            var length = endLocal - startLocal;
            var granularity = (query.Granularity ?? string.Empty).Trim().ToLowerInvariant();
            if (granularity.Length == 0)
                granularity = length.TotalDays <= 1 ? "hour" : length.TotalDays <= 92 ? "day" : "week";
            if (granularity is not ("hour" or "day" or "week" or "month"))
                return (null, "granularity phải là hour, day, week hoặc month.", "InvalidGranularity");

            var buckets = new List<(DateTime, DateTime, string)>();
            var cursor = granularity switch
            {
                "week" => startLocal.Date.AddDays(-(((int)startLocal.DayOfWeek + 6) % 7)),
                "month" => new DateTime(startLocal.Year, startLocal.Month, 1),
                _ => startLocal
            };
            while (cursor < endLocal)
            {
                var next = granularity switch
                {
                    "hour" => cursor.AddHours(1), "day" => cursor.AddDays(1), "week" => cursor.AddDays(7), _ => cursor.AddMonths(1)
                };
                var bucketStart = cursor < startLocal ? startLocal : cursor;
                var bucketEnd = next > endLocal ? endLocal : next;
                var label = granularity switch
                {
                    "hour" => bucketStart.ToString("dd/MM HH:00", CultureInfo.InvariantCulture),
                    "day" => bucketStart.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    "week" => $"{bucketStart:dd/MM}-{bucketEnd.AddDays(-1):dd/MM}",
                    _ => bucketStart.ToString("MM/yyyy", CultureInfo.InvariantCulture)
                };
                buckets.Add((Utc(bucketStart - Vn), Utc(bucketEnd - Vn), label));
                if (buckets.Count > MaxBuckets)
                    return (null, $"Quá nhiều điểm dữ liệu (tối đa {MaxBuckets}); chọn đơn vị thời gian lớn hơn.", "TooManyBuckets");
                cursor = next;
            }
            var start = Utc(startLocal - Vn);
            var end = Utc(endLocal - Vn);
            var asOf = nowUtc < end ? (nowUtc < start ? start : nowUtc) : end;
            return (new Plan(period, granularity, start, end, asOf, length, buckets), null, null);
        }

        // ---------- data ----------

        private async Task<MerchantReportDto> BuildAsync(Plan plan, int top)
        {
            var merchantId = CurrentUser.Id!.Value;
            var current = new Range(plan.Start, plan.AsOf);
            var previous = new Range(plan.Start - plan.Length, plan.AsOf - plan.Length);
            var hourly = plan.Granularity == "hour";
            var unit = hourly ? 3600L : 86400L;
            var finance = LazyServiceProvider.LazyGetRequiredService<MerchantFinanceAggregator>();

            // Every number below is a SQL aggregate read inside one repeatable-read transaction (one snapshot).
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var cur = await finance.TotalsAsync(merchantId, current.From, current.To);
            var prev = await finance.TotalsAsync(merchantId, previous.From, previous.To);
            var curMoney = await finance.SeriesAsync(merchantId, current.From, current.To, hourly);
            var prevMoney = await finance.SeriesAsync(merchantId, previous.From, previous.To, hourly);

            var orders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted && o.MerchantId == merchantId);
            var timeline = await _timelineRepo.GetQueryableAsync();
            async Task<Dictionary<long, int>> CreatedAsync(Range r)
            {
                long fromTs = CeilUnix(r.From), toTs = CeilUnix(r.To);
                return (await AsyncExecuter.ToListAsync(orders.Where(o => o.CreatedOn >= fromTs && o.CreatedOn < toTs)
                        .GroupBy(o => (o.CreatedOn + 25200) / unit).Select(g => new { g.Key, Count = g.Count() })))
                    .ToDictionary(x => x.Key, x => x.Count);
            }
            async Task<Dictionary<long, int>> CompletedAsync(Range r)
            {
                long fromTs = CeilUnix(r.From), toTs = CeilUnix(r.To);
                return (await AsyncExecuter.ToListAsync(
                        (from t in timeline
                         join o in orders on t.WashOrderId equals o.Id
                         where t.Status == WashOrderStatus.Completed && t.Timestamp >= fromTs && t.Timestamp < toTs
                         select t.Timestamp)
                        .GroupBy(ts => (ts + 25200) / unit).Select(g => new { g.Key, Count = g.Count() })))
                    .ToDictionary(x => x.Key, x => x.Count);
            }
            Task<int> CancelledAsync(Range r) =>
                AsyncExecuter.CountAsync(orders.Where(o => o.CancelledAt != null && o.CancelledAt >= r.From && o.CancelledAt < r.To));
            async Task<(int OnTime, int Total)> OnTimeAsync(Range r)
            {
                long fromTs = CeilUnix(r.From), toTs = CeilUnix(r.To);
                var rows = from t in timeline
                           join o in orders on t.WashOrderId equals o.Id
                           where t.Status == WashOrderStatus.AwaitingPayment && o.EstimatedFinishAt != null &&
                                 t.Timestamp >= fromTs && t.Timestamp < toTs
                           select new { t.Timestamp, Eta = o.EstimatedFinishAt!.Value };
                // ETA is stored to the microsecond, the history to the second: compare at second precision.
                var onTime = await AsyncExecuter.CountAsync(rows.Where(x => DateTime.UnixEpoch.AddSeconds(x.Timestamp) <= x.Eta.AddSeconds(1)));
                return (onTime, await AsyncExecuter.CountAsync(rows));
            }
            var createdCur = await CreatedAsync(current);
            var createdPrev = await CreatedAsync(previous);
            var completedCur = await CompletedAsync(current);
            var completedPrev = await CompletedAsync(previous);
            var cancelledCur = await CancelledAsync(current);
            var cancelledPrev = await CancelledAsync(previous);
            var onTimeCur = await OnTimeAsync(current);
            var onTimePrev = await OnTimeAsync(previous);

            // Top services: distinct orders with a laundry payment in the period, ranked in SQL; only the top rows come back.
            var allOrders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted);
            async Task<IQueryable<Guid>> PaidOrderIdsAsync(Range r) =>
                // Same Merchant attribution as the money totals: the Merchant snapshotted on the transaction (legacy: current).
                (from p in await _paymentRepo.GetQueryableAsync()
                 join o in allOrders on p.OrderId equals o.Id
                 where p.Purpose == PaymentPurpose.Order && p.CompletedAt != null && p.CompletedAt >= r.From && p.CompletedAt < r.To &&
                       (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded) &&
                       (p.FinancialSnapshotBasis != null ? p.MerchantIdSnapshot : o.MerchantId) == merchantId
                 select o.Id).Distinct();
            var items = await _itemRepo.GetQueryableAsync();
            var curPaid = await PaidOrderIdsAsync(current);
            var ranking = await AsyncExecuter.ToListAsync(items.Where(i => curPaid.Contains(i.WashOrderId))
                .GroupBy(i => i.ServiceId)
                .Select(g => new
                {
                    ServiceId = g.Key, Name = g.Max(x => x.ServiceName), Orders = g.Select(x => x.WashOrderId).Distinct().Count(),
                    Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.UnitPrice * x.Quantity)
                })
                .OrderByDescending(x => x.Orders).ThenByDescending(x => x.Value).ThenBy(x => x.ServiceId).Take(top));
            var topIds = ranking.Select(x => x.ServiceId).ToList();
            var prevPaid = await PaidOrderIdsAsync(previous);
            var prevCounts = (await AsyncExecuter.ToListAsync(items.Where(i => topIds.Contains(i.ServiceId) && prevPaid.Contains(i.WashOrderId))
                    .GroupBy(i => i.ServiceId).Select(g => new { g.Key, Orders = g.Select(x => x.WashOrderId).Distinct().Count() })))
                .ToDictionary(x => x.Key, x => x.Orders);
            var services = (await AsyncExecuter.ToListAsync((await _serviceRepo.GetQueryableAsync())
                    .Where(s => topIds.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.Code, s.Images })))
                .ToDictionary(x => x.Id);
            await uow.CompleteAsync();

            var complete = cur.CommissionComplete && prev.CommissionComplete;
            KpiValueDto Estimated(KpiValueDto k)
            {
                // Missing commission snapshots make commission and payout provisional: no comparison percentage.
                if (complete) return k;
                k.IsEstimated = true;
                k.ChangePercent = null;
                // Without full coverage a zero baseline proves nothing: never report "new".
                k.IsNew = false;
                return k;
            }
            RateKpiDto OnTime()
            {
                decimal? Rate(int n, int d) => d == 0 ? null : Math.Round(n * 100m / d, 1);
                var dto = new RateKpiDto
                {
                    Numerator = onTimeCur.OnTime, Denominator = onTimeCur.Total,
                    PreviousNumerator = onTimePrev.OnTime, PreviousDenominator = onTimePrev.Total
                };
                dto.Rate = Rate(dto.Numerator, dto.Denominator);
                dto.PreviousRate = Rate(dto.PreviousNumerator, dto.PreviousDenominator);
                dto.ChangePoints = dto.Rate.HasValue && dto.PreviousRate.HasValue ? dto.Rate - dto.PreviousRate : null;
                dto.UnavailableReason = dto.Denominator == 0 ? "Chưa có đơn giặt xong có ETA trong kỳ." : null;
                return dto;
            }

            var report = new MerchantReportDto
            {
                Period = plan.Period,
                Granularity = plan.Granularity,
                Current = RangeDto(plan.Start, plan.End),
                Previous = RangeDto(plan.Start - plan.Length, plan.End - plan.Length),
                Incomplete = plan.AsOf < plan.End,
                AsOf = plan.AsOf,
                GeneratedAt = DateTime.UtcNow,
                Kpis = new MerchantReportKpisDto
                {
                    GrossLaundryRevenue = Kpi(cur.Gross, prev.Gross),
                    Refunds = Kpi(cur.Refunds, prev.Refunds),
                    NetRevenue = Kpi(cur.Net, prev.Net),
                    Commission = Estimated(Kpi(cur.Commission, prev.Commission)),
                    MerchantPayout = Estimated(Kpi(cur.Payout, prev.Payout)),
                    FeesCollected = Kpi(cur.Fees, prev.Fees),
                    OrdersCreated = Kpi(createdCur.Values.Sum(), createdPrev.Values.Sum()),
                    OrdersCompleted = Kpi(completedCur.Values.Sum(), completedPrev.Values.Sum()),
                    OrdersCancelled = Kpi(cancelledCur, cancelledPrev),
                    OnTimeRate = OnTime(),
                    OrdersWithoutCommissionSnapshot = cur.UncoveredOrders,
                    CommissionCoverage = new CommissionCoverageDto
                    {
                        CurrentComplete = cur.CommissionComplete, PreviousComplete = prev.CommissionComplete,
                        CurrentUncoveredOrders = cur.UncoveredOrders, PreviousUncoveredOrders = prev.UncoveredOrders,
                        CurrentUncoveredNetAmount = cur.UncoveredAmount, PreviousUncoveredNetAmount = prev.UncoveredAmount
                    }
                },
                Definitions = Definitions
            };

            // Buckets are filled from the per-hour/per-day SQL aggregates (local UTC+7 keys).
            decimal NetIn(List<MerchantFinanceAggregator.Bucket> rows, DateTime from, DateTime to) =>
                rows.Where(b => Utc(b.LocalStart - Vn) >= from && Utc(b.LocalStart - Vn) < to).Sum(b => b.Gross - b.Refunds);
            int CountIn(Dictionary<long, int> rows, DateTime from, DateTime to) =>
                rows.Where(x => Utc(DateTime.UnixEpoch.AddSeconds(x.Key * unit) - Vn) >= from &&
                                Utc(DateTime.UnixEpoch.AddSeconds(x.Key * unit) - Vn) < to).Sum(x => x.Value);
            for (var i = 0; i < plan.Buckets.Count; i++)
            {
                var (bStart, bEnd, label) = plan.Buckets[i];
                var computed = bStart < plan.AsOf;
                report.Series.Add(new ReportBucketDto
                {
                    Index = i, Label = label, Start = bStart, End = bEnd,
                    PreviousStart = bStart - plan.Length, PreviousEnd = bEnd - plan.Length,
                    Computed = computed,
                    NetRevenue = computed ? NetIn(curMoney, bStart, bEnd) : 0,
                    PreviousNetRevenue = computed ? NetIn(prevMoney, bStart - plan.Length, bEnd - plan.Length) : 0,
                    OrdersCreated = computed ? CountIn(createdCur, bStart, bEnd) : 0,
                    OrdersCompleted = computed ? CountIn(completedCur, bStart, bEnd) : 0
                });
            }

            var rank = 0;
            foreach (var row in ranking)
            {
                var prevCount = prevCounts.GetValueOrDefault(row.ServiceId);
                var info = services.GetValueOrDefault(row.ServiceId);
                report.TopServices.Add(new TopServiceDto
                {
                    Rank = ++rank, ServiceId = row.ServiceId, Name = info?.Name ?? row.Name, Code = info?.Code,
                    ImageUrls = ReadImages(info?.Images), OrderCount = row.Orders, PreviousOrderCount = prevCount,
                    OrderCountChangePercent = Kpi(row.Orders, prevCount).ChangePercent, Quantity = row.Quantity, ListValue = row.Value
                });
            }
            return report;
        }

        public async Task<BaseResponse<MerchantLifetimeFinanceDto>> GetLifetimeAsync()
        {
            // All parts of the totals come from one repeatable-read snapshot.
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var totals = await LazyServiceProvider.LazyGetRequiredService<MerchantFinanceAggregator>()
                .TotalsAsync(CurrentUser.Id!.Value, null, null);
            await snapshot.CompleteAsync();
            return Success(new MerchantLifetimeFinanceDto
            {
                GrossLaundryRevenue = totals.Gross, Refunds = totals.Refunds, NetRevenue = totals.Net,
                Commission = totals.Commission, MerchantPayout = totals.Payout, FeesCollected = totals.Fees,
                PaidOrderCount = totals.PaidOrders, CommissionComplete = totals.CommissionComplete,
                OrdersWithoutCommissionSnapshot = totals.UncoveredOrders, UncoveredNetAmount = totals.UncoveredAmount,
                GeneratedAt = DateTime.UtcNow
            });
        }

        private static readonly Dictionary<string, string> Definitions = new()
        {
            ["timezone"] = "Mốc ngày/giờ theo giờ Việt Nam (UTC+7); khoảng [from, toExclusive).",
            ["previous"] = "Kỳ so sánh liền trước, cùng độ dài; kỳ chưa kết thúc thì so cùng tiến độ (tới cùng thời điểm tương ứng).",
            ["grossLaundryRevenue"] = "Tiền giặt khách đã trả cho đơn của tiệm, theo ngày giao dịch thành công.",
            ["refunds"] = "Tiền đã hoàn cho khách của đơn của tiệm, theo ngày hoàn tiền.",
            ["netRevenue"] = "Doanh thu thuần = tiền giặt − tiền hoàn.",
            ["commission"] = "Hoa hồng sàn theo tỷ lệ chốt trên từng đơn (0% là hợp lệ); tiền hoàn giảm hoa hồng theo cùng tỷ lệ. Đơn chưa có tỷ lệ chốt không được tính và không dùng tỷ lệ hiện tại: commissionCoverage cho biết từng kỳ đã đủ chưa; chưa đủ thì commission/merchantPayout là tạm tính (isEstimated) và không có % thay đổi.",
            ["merchantPayout"] = "Thu nhập phát sinh của tiệm = doanh thu thuần − hoa hồng; chưa phải số đã được chi trả qua kỳ đối soát.",
            ["feesCollected"] = "Phí hủy và phí lưu kho/quá hạn đã thu; không tính vào doanh thu tiệm.",
            ["ordersCreated"] = "Số đơn của tiệm được tạo trong kỳ (ngày tạo đơn).",
            ["ordersCompleted"] = "Số đơn của tiệm chuyển sang Hoàn thành trong kỳ.",
            ["ordersCancelled"] = "Số đơn của tiệm bị hủy trong kỳ.",
            ["onTimeRate"] = "Tỷ lệ (%) đơn giặt xong đúng hạn: thời điểm chuyển sang Chờ thanh toán ≤ ETA đã chốt; mẫu số = đơn giặt xong trong kỳ có ETA. changePoints là chênh lệch điểm phần trăm.",
            ["changePercent"] = "(kỳ này − kỳ trước) / kỳ trước × 100; null và isNew = true khi kỳ trước bằng 0 mà kỳ này khác 0.",
            ["topServices"] = "Xếp theo số đơn khác nhau có giao dịch tiền giặt trong kỳ, rồi giá trị dịch vụ; listValue = đơn giá snapshot × số lượng, trước phụ phí cân và giảm giá."
        };

        // Local dates are built without a kind; every instant sent to PostgreSQL must be UTC.
        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static long CeilUnix(DateTime utc) =>
            (long)Math.Ceiling((DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds);

        private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

        private static KpiValueDto Kpi(decimal current, decimal previous) => new()
        {
            Current = current,
            Previous = previous,
            ChangePercent = previous == 0 ? (current == 0 ? 0 : null) : Math.Round((current - previous) / Math.Abs(previous) * 100m, 1),
            IsNew = previous == 0 && current != 0
        };

        private static ReportRangeDto RangeDto(DateTime from, DateTime toExclusive) => new()
        {
            From = from,
            ToExclusive = toExclusive,
            FromLocalDate = (from + Vn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToLocalDate = (toExclusive + Vn).AddTicks(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        private static List<string> ReadImages(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
            catch (JsonException) { return []; }
        }

        // ---------- CSV ----------

        private static byte[] ToCsv(MerchantReportDto r)
        {
            var sb = new StringBuilder();
            void Row(params object?[] cells) => sb.Append(string.Join(",", cells.Select(Cell))).Append("\r\n");
            string Money(decimal? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "unavailable";
            string Pct(decimal? v, bool isNew) => isNew ? "new" : v?.ToString("0.#", CultureInfo.InvariantCulture) ?? "unavailable";

            Row("Báo cáo kinh doanh Merchant");
            Row("Kỳ này", r.Current.FromLocalDate, r.Current.ToLocalDate);
            Row("Kỳ trước", r.Previous.FromLocalDate, r.Previous.ToLocalDate);
            Row("Múi giờ", r.Timezone);
            Row("Đơn vị tiền", r.Currency);
            Row("Số liệu tính tới (UTC)", r.AsOf.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), r.Incomplete ? "kỳ chưa kết thúc" : "đủ kỳ");
            Row("Tạo lúc (UTC)", r.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Row();
            Row("Chỉ số", "Kỳ này", "Kỳ trước", "Thay đổi (%)");
            void KpiRow(string name, KpiValueDto k) => Row(name, Money(k.Current), Money(k.Previous), Pct(k.ChangePercent, k.IsNew));
            KpiRow("Tiền giặt", r.Kpis.GrossLaundryRevenue);
            KpiRow("Tiền hoàn", r.Kpis.Refunds);
            KpiRow("Doanh thu thuần", r.Kpis.NetRevenue);
            KpiRow(r.Kpis.Commission.IsEstimated ? "Hoa hồng (tạm tính, thiếu tỷ lệ chốt)" : "Hoa hồng", r.Kpis.Commission);
            KpiRow(r.Kpis.MerchantPayout.IsEstimated ? "Thu nhập phát sinh (tạm tính, chưa phải số đã chi trả)" : "Thu nhập phát sinh (chưa phải số đã chi trả)", r.Kpis.MerchantPayout);
            KpiRow("Phí hủy/lưu kho đã thu", r.Kpis.FeesCollected);
            KpiRow("Đơn tạo", r.Kpis.OrdersCreated);
            KpiRow("Đơn hoàn thành", r.Kpis.OrdersCompleted);
            KpiRow("Đơn hủy", r.Kpis.OrdersCancelled);
            var t = r.Kpis.OnTimeRate;
            Row("Tỷ lệ đúng hạn (%)", Money(t.Rate), Money(t.PreviousRate),
                t.ChangePoints.HasValue ? $"{t.ChangePoints.Value.ToString("0.#", CultureInfo.InvariantCulture)} điểm" : "unavailable");
            Row("Đúng hạn / tổng (kỳ này)", $"{t.Numerator}/{t.Denominator}");
            var cov = r.Kpis.CommissionCoverage;
            Row("Đơn chưa có tỷ lệ hoa hồng chốt", cov.CurrentUncoveredOrders, cov.PreviousUncoveredOrders);
            Row("Doanh thu thuần chưa xác định hoa hồng", Money(cov.CurrentUncoveredNetAmount), Money(cov.PreviousUncoveredNetAmount));
            Row();
            Row("Mốc", "Bắt đầu (UTC)", "Doanh thu thuần", "Doanh thu thuần kỳ trước", "Đơn tạo", "Đơn hoàn thành");
            foreach (var b in r.Series)
                Row(b.Label, b.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    b.Computed ? Money(b.NetRevenue) : "unavailable", b.Computed ? Money(b.PreviousNetRevenue) : "unavailable",
                    b.Computed ? b.OrdersCreated : "unavailable", b.Computed ? b.OrdersCompleted : "unavailable");
            Row();
            Row("Hạng", "Mã dịch vụ", "Tên dịch vụ", "Số đơn", "Số đơn kỳ trước", "Thay đổi (%)", "Số lượng", "Giá trị theo giá snapshot");
            foreach (var s in r.TopServices)
                Row(s.Rank, s.Code, s.Name, s.OrderCount, s.PreviousOrderCount,
                    s.OrderCountChangePercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? (s.PreviousOrderCount == 0 ? "new" : "unavailable"),
                    s.Quantity, Money(s.ListValue));
            Row();
            Row("Định nghĩa");
            foreach (var d in r.Definitions)
                Row(d.Key, d.Value);
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            return [.. preamble, .. body];
        }

        /// <summary>Escapes a CSV cell and neutralises spreadsheet formulas in text that comes from user data.</summary>
        private static string Cell(object? value)
        {
            var text = value switch
            {
                null => string.Empty,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
            if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0]) &&
                !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                text = "'" + text;
            return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }
}
