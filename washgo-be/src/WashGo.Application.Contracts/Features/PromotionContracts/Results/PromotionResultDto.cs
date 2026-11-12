using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Results
{
    public class PromotionResultDto
    {
    public string Version { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    /// <summary>false khi mã đã từng được dùng (kể cả lượt đã hoàn tác): server chặn đổi mã.</summary>
    public bool CanChangeCode { get; set; } = true;
    public string? ChangeCodeReasonCode { get; set; }
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsageLimitPerCustomer { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; }
        public PromotionStatus Status { get; set; }
        public long CreatedOn { get; set; }
    }
}
