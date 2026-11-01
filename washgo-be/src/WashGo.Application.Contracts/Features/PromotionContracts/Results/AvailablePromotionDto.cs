using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Results
{
    /// <summary>Mã giảm giá hiển thị cho khách ở bước xác nhận đơn.</summary>
    public class AvailablePromotionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime EndAt { get; set; }
        public int? RemainingUsesForCustomer { get; set; }
        public bool IsEligible { get; set; }
        public string? IneligibleReason { get; set; }
        public decimal EstimatedDiscountAmount { get; set; }
    }
}
