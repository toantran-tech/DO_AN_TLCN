using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Core.Domain.Shared.Enums;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashService
{
    [SevagoTable(WashGoDbProperties.Services, Schema = WashGoDbProperties.Schema)]
    public class WashService : BaseEntity, IHasAuditLog
    {
        public WashService() { }
        public WashService(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string Code { get; set; } = string.Empty;

        [Filterable(ColumnVariant.Text)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }

        [Filterable(ColumnVariant.MultiSelect)]
        public WashServiceType ServiceType { get; set; } = WashServiceType.StandardWash;

        public string Unit { get; set; } = "kg";
        public int EstimatedDurationHours { get; set; } = 24;
        public string? AllowedStatuses { get; set; }
        public string? Images { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; } = ServiceApprovalStatus.Pending;
        public string? RejectionReason { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}