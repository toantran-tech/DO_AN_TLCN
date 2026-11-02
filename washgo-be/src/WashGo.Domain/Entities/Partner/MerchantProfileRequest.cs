using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{
    /// <summary>UC 2.1.2: yêu cầu Merchant cập nhật hồ sơ cửa hàng, chỉ áp dụng khi Admin phê duyệt.</summary>
    [SevagoTable(WashGoDbProperties.MerchantProfileRequests, Schema = WashGoDbProperties.Schema)]
    public class MerchantProfileRequest : Entity<Guid>
    {
        public MerchantProfileRequest() { }
        public MerchantProfileRequest(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }
        public string ProposedData { get; set; } = "{}";
        public string CurrentData { get; set; } = "{}";
        public ServiceApprovalStatus Status { get; set; } = ServiceApprovalStatus.Pending;
        public string? RejectionReason { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
