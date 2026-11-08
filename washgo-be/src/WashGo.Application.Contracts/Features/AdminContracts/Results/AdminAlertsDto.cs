using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AdminContracts.Results
{
    public class AdminAlertDto
    {
        public AdminAlertType Type { get; set; }
        public AdminAlertSeverity Severity { get; set; }
        /// <summary>Luôn "Rule": cảnh báo suy ra từ quy tắc trên dữ liệu hiện tại, không phải dự báo/AI.</summary>
        public string Source { get; set; } = "Rule";
        public string Title { get; set; } = string.Empty;
        /// <summary>Order hoặc Locker.</summary>
        public string ResourceType { get; set; } = string.Empty;
        public Guid ResourceId { get; set; }
        public string ResourceCode { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public string? LockerName { get; set; }
        /// <summary>Hạn chót đã vượt (UTC); với Locker là thời điểm kiểm tra.</summary>
        public DateTime Deadline { get; set; }
        public int OverdueMinutes { get; set; }
        /// <summary>Locker: số ô trống / tổng ô.</summary>
        public int? AvailableBoxes { get; set; }
        public int? TotalBoxes { get; set; }
    }

    public class AdminAlertCountDto
    {
        public AdminAlertType Type { get; set; }
        public AdminAlertSeverity Severity { get; set; }
        public int Count { get; set; }
    }

    public class AdminAlertsDto
    {
        public DateTime GeneratedAt { get; set; }
        public int Page { get; set; }
        public int Take { get; set; }
        /// <summary>Tổng theo bộ lọc type/severity (để phân trang).</summary>
        public int Total { get; set; }
        /// <summary>Tổng mọi cảnh báo, không lọc.</summary>
        public int TotalAll { get; set; }
        public List<AdminAlertCountDto> Summary { get; set; } = [];
        /// <summary>Mức độ cao trước, rồi hạn chót sớm nhất (quá hạn lâu nhất), rồi Id.</summary>
        public List<AdminAlertDto> Items { get; set; } = [];
    }
}
