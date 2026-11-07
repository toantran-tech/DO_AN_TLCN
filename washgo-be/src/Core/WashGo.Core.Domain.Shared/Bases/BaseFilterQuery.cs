using System.Collections.Generic;

namespace WashGo.Core.Domain.Shared.Bases
{
    public class BaseFilterQuery
    {
        public int Page { get; set; } = 0;
        public int Take { get; set; } = 100;
        public SortOption[] SortBy { get; set; } = [];
        public string[] SelectFields { get; set; } = [];
        public long FromDate { get; set; }
        public long ToDate { get; set; }
        public string? Keyword { get; set; }
        public Dictionary<string, string> Filters { get; set; } = [];
        public List<ColumnFilterDto> ColumnFilters { get; set; } = [];

        /// <summary>Latest accepted Unix second (31/12/9999 23:59:59 UTC).</summary>
        public const long MaxUnixSeconds = 253402300799;

        /// <summary>FromDate/ToDate are Unix seconds, inclusive, 0 = no bound; null when the range is valid.</summary>
        public string? DateRangeError() =>
            FromDate < 0 || ToDate < 0 || FromDate > MaxUnixSeconds || ToDate > MaxUnixSeconds
                ? "Khoảng ngày không hợp lệ (fromDate/toDate là Unix giây, không âm)."
                : FromDate > 0 && ToDate > 0 && FromDate > ToDate
                    ? "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
                    : null;
    }

    public class ColumnFilterDto
    {
        public string Field { get; set; } = string.Empty;
        public string? DataType { get; set; }
        public List<string> SelectedValues { get; set; } = [];
        public ColumnFilterConditionDto? Condition { get; set; }
    }

    public class ColumnFilterConditionDto
    {
        public string? Operator { get; set; }
        public string? Value { get; set; }
        public string? ValueTo { get; set; }
    }

    public class SortOption
    {
        public string Field { get; set; } = string.Empty;
        public bool IsDescending { get; set; }
    }

    public class ColumnFilterDistinctValueDto
    {
        public object? Value { get; set; }
        public string? Label { get; set; }
        public long Count { get; set; }
    }
}
