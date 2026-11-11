using System.Globalization;
using System.Net;
using System.Data;
using System.Text;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class ShipperDeliveryAppService(ShipperHistoryReader reader) : WashGoAppService, IShipperDeliveryAppService
{
    private string? Prepare(DeliveryHistoryQuery query)
    {
        if (ShipperReadRules.Validate(query) is { } error) return error;
        var range = ShipperReadRules.Range(query.FromDate, query.ToDate, DateTime.UtcNow);
        if (range.Error != null) return range.Error;
        query.FromDate = range.From; query.ToDate = range.To;
        return null;
    }

    public async Task<PaginatedResult<DeliveryHistoryDto>> GetHistoryAsync(DeliveryHistoryQuery query)
    {
        if (Prepare(query) is { } error)
            return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = error };
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
        var rows = await reader.QueryAsync(CurrentUser.Id ?? Guid.Empty, query);
        var total = await AsyncExecuter.CountAsync(rows);
        var tasks = await AsyncExecuter.ToListAsync(ShipperHistoryReader.Sort(rows, query).Skip((query.Page - 1) * query.Take).Take(query.Take));
        var list = await reader.MapAsync(tasks);
        await uow.CompleteAsync();
        return Success(list, total, query.Page, query.Take);
    }

    public async Task<BaseResponse<DeliverySummaryDto>> GetSummaryAsync(DeliveryHistoryQuery query)
    {
        if (Prepare(query) is { } error) return BadRequest<DeliverySummaryDto>(error);
        var rows = await reader.QueryAsync(CurrentUser.Id ?? Guid.Empty, query, ignoreType: true);
        var counts = await AsyncExecuter.FirstOrDefaultAsync(rows.GroupBy(x => 1).Select(g => new
        {
            Total = g.Count(), Collect = g.Count(x => x.TaskType == DeliveryTaskType.Collect),
            Captured = g.Count(x => x.SnapshotSource == "CapturedAtCompletion")
        }));
        return Success(new DeliverySummaryDto
        {
            Total = counts?.Total ?? 0, CollectCount = counts?.Collect ?? 0,
            ReturnCount = (counts?.Total ?? 0) - (counts?.Collect ?? 0), CapturedSnapshotCount = counts?.Captured ?? 0,
            FromDate = query.FromDate, ToDate = query.ToDate, GeneratedAt = DateTime.UtcNow,
            Keyword = query.Keyword, LockerId = query.LockerId, CancelledReturn = query.CancelledReturn
        });
    }

    public async Task<BaseResponse<DeliveryHistoryDto>> GetEventAsync(Guid eventId)
    {
        var row = await AsyncExecuter.FirstOrDefaultAsync((await reader.QueryAsync(CurrentUser.Id ?? Guid.Empty, new()))
            .Where(x => x.CompletionEventId == eventId));
        return row == null ? NotFound<DeliveryHistoryDto>("Không tìm thấy lượt giao nhận.") : Success((await reader.MapAsync([row])).Single());
    }

    public async Task<BaseResponse<List<DeliveryLockerDto>>> GetLockersAsync(DeliveryHistoryQuery query)
    {
        if (Prepare(query) is { } error) return BadRequest<List<DeliveryLockerDto>>(error);
        query.LockerId = null;
        var rows = await reader.QueryAsync(CurrentUser.Id ?? Guid.Empty, query);
        var list = await AsyncExecuter.ToListAsync(rows.Where(x => x.LockerIdSnapshot != null)
            .GroupBy(x => x.LockerIdSnapshot).Select(g => new DeliveryLockerDto
            { Id = g.Key!.Value, Name = g.Max(x => x.LockerNameSnapshot) ?? string.Empty }).OrderBy(x => x.Name).ThenBy(x => x.Id).Take(1000));
        return Success(list);
    }

    public async Task<BaseResponse<string>> ExportAsync(DeliveryHistoryQuery query)
    {
        if (Prepare(query) is { } error) return BadRequest<string>(error);
        var rows = await reader.QueryAsync(CurrentUser.Id ?? Guid.Empty, query);
        var data = await AsyncExecuter.ToListAsync(ShipperHistoryReader.Sort(rows, query).Take(ShipperReadRules.ExportLimit + 1));
        if (data.Count > ShipperReadRules.ExportLimit)
            return new() { Succeeded = false, StatusCode = HttpStatusCode.RequestEntityTooLarge, Message = "Vượt 25.000 lượt; vui lòng thu hẹp bộ lọc.", Meta = new { code = "ReportTooLarge" } };
        var csv = new StringBuilder("EventId,OrderCode,MerchantOrderCode,TaskType,Locker,Address,Box,CompletedAt,EventStatus\r\n");
        foreach (var x in data)
            csv.AppendLine(string.Join(",", new[] { x.CompletionEventId.ToString(), x.OrderCodeSnapshot, x.MerchantOrderCodeSnapshot,
                x.TaskType.ToString(), x.LockerNameSnapshot, x.LockerAddressSnapshot, x.BoxNumberSnapshot,
                x.CompletedAt?.ToString("O", CultureInfo.InvariantCulture), x.OrderStatusSnapshot?.ToString() }.Select(Csv)));
        return Success(csv.ToString());
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
