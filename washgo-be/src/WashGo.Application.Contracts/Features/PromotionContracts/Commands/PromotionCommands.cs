using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Commands
{
    public class CreatePromotionCommand
    {
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsageLimitPerCustomer { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdatePromotionCommand : CreatePromotionCommand
    {
        public Guid Id { get; set; }
    }
}
