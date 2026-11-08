using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Feedback
{
    /// <summary>Báo cáo sự cố (khiếu nại) của khách về một đơn đã hoàn thành.</summary>
    [SevagoTable(WashGoDbProperties.Incidents, Schema = WashGoDbProperties.Schema)]
    public class Incident : BaseEntity
    {
        /// <summary>Khách chỉ được báo sự cố trong số ngày này kể từ khi nhận đồ.</summary>
        public const int ReportWindowDays = 3;

        public Guid OrderId { get; set; }
        public long Revision { get; set; }
        public int? CompensationPercentApplied { get; set; }
        public string? CompensationSettingsVersion { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
        public IncidentStatus Status { get; set; } = IncidentStatus.Pending;
        public string? ResolutionNote { get; set; }
        public ResponsibleParty? ResponsibleParty { get; set; }
        public CompensationLevel CompensationLevel { get; set; }
        public decimal ItemValue { get; set; }
        public decimal CompensationAmount { get; set; }
        public CompensationStatus CompensationStatus { get; set; }
        public string? CompensationReference { get; set; }
        public DateTime? CompensationPaidAt { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        /// <summary>Mã yêu cầu do client sinh để gửi lại không tạo khiếu nại trùng (idempotency key).</summary>
        public string? ClientRequestId { get; set; }
        public DateTime? CustomerUpdatedAt { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public string? WithdrawReason { get; set; }

        /// <summary>Tiền bồi thường = giá trị đồ x % theo mức hư hỏng (cấu hình ở UC 3.9.4), làm tròn đến đồng.</summary>
        public static decimal CalculateCompensation(int percent, decimal itemValue) =>
            decimal.Round(itemValue * percent / 100m, 0, MidpointRounding.AwayFromZero);
    }
}
