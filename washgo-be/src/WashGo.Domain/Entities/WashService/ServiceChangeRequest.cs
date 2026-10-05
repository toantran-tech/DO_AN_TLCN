using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashService
{
    [SevagoTable(WashGoDbProperties.ServiceChangeRequests, Schema = WashGoDbProperties.Schema)]
    public class ServiceChangeRequest : Entity<Guid>
    {
        public Guid MerchantId { get; set; }
        public Guid? ServiceId { get; set; }
        public ServiceChangeRequestType RequestType { get; set; }
        public string ProposedData { get; set; } = "{}";
        public string? CurrentData { get; set; }
        public string? Reason { get; set; }
        public ServiceApprovalStatus Status { get; set; } = ServiceApprovalStatus.Pending;
        public string? RejectionReason { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}