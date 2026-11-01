using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Queries
{
    /// <summary>FromDate/ToDate (Unix giây) lọc các mã có thời gian hiệu lực giao với khoảng này.</summary>
    public class PromotionFilterQuery : BaseFilterQuery
    {
        public PromotionStatus? Status { get; set; }
        public DiscountType? DiscountType { get; set; }
    }
}
