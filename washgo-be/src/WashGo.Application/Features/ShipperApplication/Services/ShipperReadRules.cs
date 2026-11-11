using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

public static class ShipperReadRules
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";
    public const int ExportLimit = 25000;

    public static string? Validate(DeliveryHistoryQuery query)
    {
        query.SortBy ??= [];
        if (query.ToDate == BaseFilterQuery.MaxUnixSeconds) return "Ngày kết thúc vượt phạm vi hỗ trợ.";
        if (query.DateRangeError() is { } error) return error;
        if (query.TaskType.HasValue && !Enum.IsDefined(query.TaskType.Value)) return "Loại nhiệm vụ không hợp lệ.";
        if (query.EventStatus.HasValue && !Enum.IsDefined(query.EventStatus.Value)) return "Trạng thái không hợp lệ.";
        if (query.Page < 1 || query.Take is < 1 or > 100) return "page phải từ 1; take từ 1 đến 100.";
        if ((long)(query.Page - 1) * query.Take > int.MaxValue) return "Trang vượt giới hạn xử lý.";
        if (query.Keyword?.Length > 200) return "Từ khóa tối đa 200 ký tự.";
        if (query.SortBy.Length > 2 || query.SortBy.Any(x => x == null || !new[] { "CompletedAt", "OrderCode" }.Contains(x.Field, StringComparer.OrdinalIgnoreCase)))
            return "Chỉ sắp xếp CompletedAt hoặc OrderCode.";
        return null;
    }

    public static (long From, long To, string? Error) Range(long from, long to, DateTime now)
    {
        var end = to == 0 ? new DateTimeOffset(now).ToUnixTimeSeconds() : to;
        var start = from == 0 ? Math.Max(0, end - 366L * 86400 + 1) : from;
        var error = start > end || end - start + 1 > 366L * 86400 ? "Khoảng thời gian tối đa 366 ngày, ngày bắt đầu không được sau ngày kết thúc." : null;
        return (start, end, error);
    }
}
