using System.Data;
using System.Net;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class ShipperDashboardAppService(IShipperRepository shippers, IRepository<Merchant, Guid> merchants,
    IRepository<DeliveryTask, Guid> tasks, IWashOrderRepository orders, ShipperHistoryReader history,
    ShipperTaskPresentation presentation) : WashGoAppService, IShipperDashboardAppService
{
    public async Task<BaseResponse<ShipperDashboardDto>> GetAsync(ShipperDashboardQuery query)
    {
        var now = DateTime.UtcNow;
        var today = now.AddHours(7).Date.AddHours(-7);
        var period = query.Period.ToLowerInvariant();
        var from = period switch { "day" => today, "week" => today.AddDays(-6), "month" => today.AddHours(7).AddDays(1 - today.AddHours(7).Day).AddHours(-7), _ => today };
        var to = now;
        if (period == "custom")
        {
            if (query.FromDate <= 0 || query.ToDate <= 0 || query.ToDate > BaseFilterQuery.MaxUnixSeconds || query.FromDate > query.ToDate)
                return BadRequest<ShipperDashboardDto>("Cần khoảng Unix giây hợp lệ cho kỳ custom.");
            from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime;
            to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime;
            // A selected current Vietnam day may end tonight: clamp to server time rather than count future work.
            if (to > now && to < today.AddDays(1)) to = now;
        }
        else if (period is not ("day" or "week" or "month")) return BadRequest<ShipperDashboardDto>("period phải là day/week/month/custom.");
        if (from > to || to > now || (to - from).TotalDays > 366) return BadRequest<ShipperDashboardDto>("Kỳ báo cáo tối đa 366 ngày và không ở tương lai.");
        var previousTo = from.AddTicks(-1);
        var previousFrom = from - (to - from) - TimeSpan.FromTicks(1);
        if (previousFrom < DateTime.UnixEpoch) previousFrom = DateTime.UnixEpoch;
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
        var shipper = await shippers.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id);
        if (shipper == null) return NotFound<ShipperDashboardDto>("Không tìm thấy hồ sơ Shipper.");
        var pendingQuery = DeliveryTaskRules.ActiveAssignments(await tasks.GetQueryableAsync(), await orders.GetQueryableAsync())
            .Where(x => x.ShipperId == shipper.Id);
        var counts = await AsyncExecuter.ToListAsync(pendingQuery.GroupBy(x => x.TaskType).Select(g => new { Type = g.Key, Count = g.Count() }));
        var pendingCollect = await AsyncExecuter.ToListAsync(pendingQuery.Where(x => x.TaskType == DeliveryTaskType.Collect).OrderBy(x => x.Deadline).ThenBy(x => x.Id).Take(5));
        var pendingReturn = await AsyncExecuter.ToListAsync(pendingQuery.Where(x => x.TaskType == DeliveryTaskType.Return).OrderBy(x => x.Deadline).ThenBy(x => x.Id).Take(5));
        var completionQuery = (await tasks.GetQueryableAsync()).Where(x => x.CompletedBy == CurrentUser.Id && x.CompletedByRole == "Shipper" &&
            x.Status == DeliveryTaskStatus.Completed && x.CompletionEventId != null && x.CompletedAt >= previousFrom && x.CompletedAt <= to);
        var events = await AsyncExecuter.ToListAsync(completionQuery.OrderBy(x => x.CompletedAt).ThenBy(x => x.Id).Take(ShipperReadRules.ExportLimit + 1));
        if (events.Count > ShipperReadRules.ExportLimit)
            return new() { Succeeded = false, StatusCode = HttpStatusCode.RequestEntityTooLarge, Message = "Quá 25.000 lượt trong kỳ và kỳ so sánh; vui lòng thu hẹp khoảng ngày.", Meta = new { code = "ReportTooLarge" } };
        var current = events.Where(x => x.CompletedAt >= from).ToList();
        var prior = events.Where(x => x.CompletedAt < from).ToList();
        var hourly = period == "day";
        var step = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var start = hourly ? from.Date.AddHours(from.Hour) : from.AddHours(7).Date.AddHours(-7);
        var chart = new List<ShipperChartPointDto>();
        for (var bucket = start; bucket <= to; bucket += step)
        {
            var next = bucket + step;
            var rows = current.Where(x => x.CompletedAt >= bucket && x.CompletedAt < next).ToList();
            chart.Add(new() { BucketStart = bucket, CollectCount = rows.Count(x => x.TaskType == DeliveryTaskType.Collect), ReturnCount = rows.Count(x => x.TaskType == DeliveryTaskType.Return) });
        }
        var result = new ShipperDashboardDto
        {
            PendingCollectCount = counts.FirstOrDefault(x => x.Type == DeliveryTaskType.Collect)?.Count ?? 0,
            PendingReturnCount = counts.FirstOrDefault(x => x.Type == DeliveryTaskType.Return)?.Count ?? 0,
            CompletedCollectCount = current.Count(x => x.TaskType == DeliveryTaskType.Collect), CompletedReturnCount = current.Count(x => x.TaskType == DeliveryTaskType.Return),
            TotalDeliveries = shipper.TotalDeliveries, Status = (int)shipper.Status, IsActive = shipper.IsActive, MerchantId = shipper.MerchantId,
            MerchantName = (await merchants.FindAsync(shipper.MerchantId))?.BusinessName, GeneratedAt = now,
            From = from, To = to, PreviousFrom = previousFrom, PreviousTo = previousTo, IsPartialPeriod = period != "custom" || query.ToDate > new DateTimeOffset(now).ToUnixTimeSeconds(),
            Performance = Performance(current), PreviousPerformance = Performance(prior), Chart = chart,
            PendingCollect = await presentation.MapAsync(pendingCollect), PendingReturn = await presentation.MapAsync(pendingReturn),
            RecentCompletions = await history.MapAsync(current.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.TaskType).ThenByDescending(x => x.CompletionEventId).Take(5).ToList())
        };
        await uow.CompleteAsync();
        return Success(result);
    }

    private static ShipperPerformanceDto Performance(List<DeliveryTask> rows)
    {
        var durations = rows.Where(x => x.AssignedAt.HasValue && x.CompletedAt >= x.AssignedAt).Select(x => (x.CompletedAt!.Value - x.AssignedAt!.Value).TotalHours).ToList();
        var sla = rows.Where(x => x.Deadline.HasValue).ToList();
        return new() { Total = rows.Count, CapturedSnapshotCount = rows.Count(x => x.SnapshotSource == "CapturedAtCompletion"),
            SlaSampleCount = sla.Count, OnTimeCount = sla.Count(x => x.CompletedAt <= x.Deadline), DurationSampleCount = durations.Count,
            AverageDurationHours = durations.Count == 0 ? null : durations.Average() };
    }
}
