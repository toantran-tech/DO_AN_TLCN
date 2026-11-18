using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;
using WashServiceEntity = WashGo.Domain.Entities.WashService.WashService;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Dự đoán thời điểm giặt xong (đơn chuyển sang "Chờ thanh toán") từ dữ liệu thật của các đơn đã xử lý trong
    /// 90 ngày: trung vị thời gian xử lý tại tiệm (lấy đồ → giặt xong) cùng tiệm và cùng loại dịch vụ, nếu ít dữ liệu
    /// thì dùng toàn hệ thống cho loại dịch vụ đó, rồi điều chỉnh theo số đơn tiệm đang xử lý. Thiếu dữ liệu thì dùng
    /// ETA theo quy tắc (giờ xử lý chuẩn của dịch vụ).
    /// </summary>
    public class EtaPredictor : ITransientDependency
    {
        public const int HistoryDays = 90;
        public const int MinSamples = 3;
        /// <summary>Mỗi đơn đang xử lý vượt quá mức này làm thời gian dự đoán tăng 5%, tối đa +50%.</summary>
        public const int NormalLoad = 5;

        private static readonly WashOrderStatus[] InProcess =
        [
            WashOrderStatus.Collected, WashOrderStatus.Washing, WashOrderStatus.Drying,
            WashOrderStatus.Ironing, WashOrderStatus.Folding
        ];

        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<WashServiceEntity, Guid> _serviceRepo;
        private readonly SystemSettingProvider _settings;
        private readonly IAsyncQueryableExecuter _executer;

        public EtaPredictor(IRepository<WashOrderTimeline, Guid> timelineRepo, IWashOrderRepository orderRepo,
            IRepository<WashOrderItem, Guid> itemRepo, IRepository<WashServiceEntity, Guid> serviceRepo,
            SystemSettingProvider settings, IAsyncQueryableExecuter executer)
        {
            _timelineRepo = timelineRepo;
            _orderRepo = orderRepo;
            _itemRepo = itemRepo;
            _serviceRepo = serviceRepo;
            _settings = settings;
            _executer = executer;
        }

        public async Task FillAsync(WashOrderDetailDto order)
        {
            var now = DateTime.UtcNow;
            order.PredictionUpdatedAt = now;
            var reached = order.Timeline.Where(x => x.FromStatus != x.Status).GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Min(x => x.Timestamp));
            if (order.Status is WashOrderStatus.Cancelled)
            {
                order.PredictionStatus = "notApplicable";
                return;
            }
            // Laundry already done: report when it actually finished.
            if (reached.TryGetValue(WashOrderStatus.AwaitingPayment, out var doneAt) || order.Status is not
                (WashOrderStatus.Pending or WashOrderStatus.Deposited or WashOrderStatus.MerchantAssigned or
                 WashOrderStatus.CollectionShipperAssigned or WashOrderStatus.Collected or WashOrderStatus.Washing or
                 WashOrderStatus.Drying or WashOrderStatus.Ironing or WashOrderStatus.Folding))
            {
                order.PredictionStatus = "done";
                order.PredictedFinishAt = reached.ContainsKey(WashOrderStatus.AwaitingPayment)
                    ? DateTimeOffset.FromUnixTimeSeconds(doneAt).UtcDateTime : order.EstimatedFinishAt;
                order.PredictionSource = "actual";
                return;
            }

            // History is grouped by service category; a category without enough history uses the rule ETA,
            // never the history of another category.
            var category = await PrimaryCategoryAsync(order.Id);
            var samples = category.HasValue ? await HistoryAsync(now) : [];
            var merchantSamples = samples.Where(x => x.MerchantId == order.MerchantId && x.Category == category).ToList();
            var categorySamples = samples.Where(x => x.Category == category).ToList();
            var (source, used) = merchantSamples.Count >= MinSamples && order.MerchantId.HasValue
                ? ("history-merchant", merchantSamples)
                : categorySamples.Count >= MinSamples ? ("history-service", categorySamples) : ("rule", []);

            if (source == "rule")
            {
                order.PredictionStatus = "rule";
                order.PredictionSource = "rule";
                order.PredictedFinishAt = order.EstimatedFinishAt
                    ?? DateTimeOffset.FromUnixTimeSeconds(order.ExpectedDeliveryTime).UtcDateTime;
                return;
            }

            var processingHours = Median(used.Select(x => x.ProcessingHours));
            var load = order.MerchantId.HasValue
                ? await _orderRepo.CountAsync(x => x.MerchantId == order.MerchantId && !x.IsDeleted && InProcess.Contains(x.Status) && x.Id != order.Id)
                : 0;
            var loadFactor = Math.Min(1.5, 1 + 0.05 * Math.Max(0, load - NormalLoad));

            // Not at the shop yet: add the usual wait from deposit to collection (or the collect SLA).
            DateTime start;
            if (reached.TryGetValue(WashOrderStatus.Collected, out var collectedAt))
                start = DateTimeOffset.FromUnixTimeSeconds(collectedAt).UtcDateTime;
            else
            {
                var collectWaits = used.Where(x => x.CollectWaitHours.HasValue).Select(x => x.CollectWaitHours!.Value).ToList();
                var wait = collectWaits.Count >= MinSamples ? Median(collectWaits) : (await _settings.GetAsync()).MerchantCollectSlaHours;
                var from = reached.TryGetValue(WashOrderStatus.Deposited, out var depositedAt)
                    ? DateTimeOffset.FromUnixTimeSeconds(depositedAt).UtcDateTime : now;
                start = from.AddHours(wait);
            }
            var predicted = start.AddHours(processingHours * loadFactor);
            order.PredictionStatus = "predicted";
            order.PredictionSource = source;
            order.PredictionSampleSize = used.Count;
            order.PredictionLoadFactor = Math.Round(loadFactor, 2);
            // A late order is still expected "soon", never in the past.
            order.PredictedFinishAt = predicted < now ? now : predicted;
        }

        /// <summary>Category of the order's main item: its snapshot, else the explicit seed mapping of a legacy type, else the service's category.</summary>
        private async Task<Guid?> PrimaryCategoryAsync(Guid orderId)
        {
            var item = await _executer.FirstOrDefaultAsync((await _itemRepo.GetQueryableAsync())
                .Where(x => x.WashOrderId == orderId).OrderByDescending(x => x.SubTotal)
                .Select(x => new { x.ServiceId, x.CategoryIdSnapshot, x.ProcessingTypeSnapshot }));
            if (item == null) return null;
            return CategoryOf(item.CategoryIdSnapshot, item.ProcessingTypeSnapshot) ?? (await _serviceRepo.FindAsync(item.ServiceId))?.CategoryId;
        }

        private static Guid? CategoryOf(Guid? snapshot, WashServiceType? legacyType) =>
            snapshot ?? (legacyType is { } t && Enum.IsDefined(t) ? ServiceCategoryRules.LegacyId((int)t) : null);

        private sealed record Sample(Guid? MerchantId, Guid? Category, double ProcessingHours, double? CollectWaitHours);

        /// <summary>Orders finished at the shop in the last 90 days: processing time Collected → AwaitingPayment.</summary>
        private async Task<List<Sample>> HistoryAsync(DateTime now)
        {
            var since = new DateTimeOffset(now.AddDays(-HistoryDays)).ToUnixTimeSeconds();
            var rows = await _executer.ToListAsync((await _timelineRepo.GetQueryableAsync())
                .Where(x => x.FromStatus != x.Status && x.Timestamp >= since - 7 * 86400 && (x.Status == WashOrderStatus.Deposited ||
                    x.Status == WashOrderStatus.Collected || x.Status == WashOrderStatus.AwaitingPayment))
                .Select(x => new { x.WashOrderId, x.Status, x.Timestamp }));
            var perOrder = rows.GroupBy(x => x.WashOrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    Deposited = g.Where(x => x.Status == WashOrderStatus.Deposited).Select(x => (long?)x.Timestamp).Min(),
                    Collected = g.Where(x => x.Status == WashOrderStatus.Collected).Select(x => (long?)x.Timestamp).Min(),
                    Done = g.Where(x => x.Status == WashOrderStatus.AwaitingPayment).Select(x => (long?)x.Timestamp).Min()
                })
                .Where(x => x.Collected.HasValue && x.Done.HasValue && x.Done > x.Collected && x.Done >= since)
                .ToList();
            if (perOrder.Count == 0) return [];

            var ids = perOrder.Select(x => x.OrderId).ToList();
            var merchants = (await _executer.ToListAsync((await _orderRepo.GetQueryableAsync())
                    .Where(x => ids.Contains(x.Id) && !x.IsDeleted).Select(x => new { x.Id, x.MerchantId })))
                .ToDictionary(x => x.Id, x => x.MerchantId);
            var mainService = (await _executer.ToListAsync((await _itemRepo.GetQueryableAsync())
                    .Where(x => ids.Contains(x.WashOrderId)).Select(x => new { x.WashOrderId, x.ServiceId, x.SubTotal, x.CategoryIdSnapshot, x.ProcessingTypeSnapshot })))
                .GroupBy(x => x.WashOrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SubTotal).First());
            var serviceIds = mainService.Values.Select(x => x.ServiceId).Distinct().ToList();
            var categories = (await _executer.ToListAsync((await _serviceRepo.GetQueryableAsync())
                    .Where(x => serviceIds.Contains(x.Id)).Select(x => new { x.Id, x.CategoryId })))
                .ToDictionary(x => x.Id, x => x.CategoryId);

            return perOrder.Where(x => merchants.ContainsKey(x.OrderId))
                .Select(x => new Sample(
                    merchants[x.OrderId],
                    mainService.TryGetValue(x.OrderId, out var primary)
                        ? CategoryOf(primary.CategoryIdSnapshot, primary.ProcessingTypeSnapshot) ?? (categories.TryGetValue(primary.ServiceId, out var c) ? c : null)
                        : null,
                    (x.Done!.Value - x.Collected!.Value) / 3600.0,
                    x.Deposited.HasValue && x.Collected > x.Deposited ? (x.Collected.Value - x.Deposited.Value) / 3600.0 : null))
                .ToList();
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        }
    }
}
