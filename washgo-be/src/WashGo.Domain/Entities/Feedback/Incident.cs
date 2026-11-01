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
        public string OrderCode { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
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

        public static decimal CompensationPercent(CompensationLevel level) => level switch
        {
            CompensationLevel.Light => 30m,
            CompensationLevel.Medium => 70m,
            CompensationLevel.Severe => 100m,
            CompensationLevel.Lost => 200m,
            _ => 0m
        };

        public static decimal CalculateCompensation(CompensationLevel level, decimal itemValue) =>
            decimal.Round(itemValue * CompensationPercent(level) / 100m, 0, MidpointRounding.AwayFromZero);
    }
}
