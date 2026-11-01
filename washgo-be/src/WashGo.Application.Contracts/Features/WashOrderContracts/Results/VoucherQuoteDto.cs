using System.Collections.Generic;
using WashGo.Application.Contracts.Features.PromotionContracts.Results;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    public class VoucherQuoteDto
    {
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public AvailablePromotionDto? AppliedPromotion { get; set; }
        public List<AvailablePromotionDto> AvailablePromotions { get; set; } = [];
    }
}
