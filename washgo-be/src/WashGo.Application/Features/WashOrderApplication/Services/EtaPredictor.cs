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
            var reached = order.Timeline.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Min(x => x.Timestamp));
            if (order.Status is WashOrderStatus.Cancelled)
            {
                order.PredictionStatus = "notApplicable";
                return;
            }
            // Laundry already done: report when it actually finished.
            if (reached.TryGetValue(WashOrderStatus.AwaitingPayment, out var doneAt) || order.Status is not
                (WashOrderStatus.Pending or WashOrderStatus.Deposited or WashOrderStatus.Collected or WashOrderStatus.Washing or
                 WashOrderStatus.Drying or WashOrderStatus.Ironing or WashOrderStatus.Folding))
            {
                order.PredictionStatus = "done";
                order.PredictedFinishAt = reached.ContainsKey(WashOrderStatus.AwaitingPayment)
                    ? DateTimeOffset.FromUnixTimeSeconds(doneAt).UtcDateTime : order.EstimatedFinishAt;
                order.PredictionSource = "actual";
                return;
            }

            var type = await PrimaryTypeAsync(order.Id);
            var samples = await HistoryAsync(now);
            var merchantSamples = samples.Where(x => x.MerchantId == order.MerchantId && x.Type == type).ToList();
            var typeSamples = samples.Where(x => x.Type == type).ToList();
            var (source, used) = merchantSamples.Count >= MinSamples && order.MerchantId.HasValue
                ? ("history-merchant", merchantSamples)
                : typeSamples.Count >= MinSamples ? ("history-service", typeSamples) : ("rule", []);

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

        private async Task<WashServiceType> PrimaryTypeAsync(Guid orderId)
        {
            var serviceId = await _executer.FirstOrDefaultAsync((await _itemRepo.GetQueryableAsync())
                .Where(x => x.WashOrderId == orderId).OrderByDescending(x => x.SubTotal).Select(x => (Guid?)x.ServiceId));
            if (!serviceId.HasValue) return WashServiceType.StandardWash;
            return (await _serviceRepo.FindAsync(serviceId.Value))?.ServiceType ?? WashServiceType.StandardWash;
        }

        private sealed record Sample(Guid? MerchantId, WashServiceType Type, double ProcessingHours, double? CollectWaitHours);

        /// <summary>Orders finished at the shop in the last 90 days: processing time Collected → AwaitingPayment.</summary>
        private async Task<List<Sample>> HistoryAsync(DateTime now)
        {
            var since = new DateTimeOffset(now.AddDays(-HistoryDays)).ToUnixTimeSeconds();
            var rows = await _executer.ToListAsync((await _timelineRepo.GetQueryableAsync())
                .Where(x => x.Timestamp >= since - 7 * 86400 && (x.Status == WashOrderStatus.Deposited ||
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
                    .Where(x => ids.Contains(x.WashOrderId)).Select(x => new { x.WashOrderId, x.ServiceId, x.SubTotal })))
                .GroupBy(x => x.WashOrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SubTotal).First().ServiceId);
            var serviceIds = mainService.Values.Distinct().ToList();
            var types = (await _executer.ToListAsync((await _serviceRepo.GetQueryableAsync())
                    .Where(x => serviceIds.Contains(x.Id)).Select(x => new { x.Id, x.ServiceType })))
                .ToDictionary(x => x.Id, x => x.ServiceType);

            return perOrder.Where(x => merchants.ContainsKey(x.OrderId))
                .Select(x => new Sample(
                    merchants[x.OrderId],
                    mainService.TryGetValue(x.OrderId, out var sid) && types.TryGetValue(sid, out var t) ? t : WashServiceType.StandardWash,
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
