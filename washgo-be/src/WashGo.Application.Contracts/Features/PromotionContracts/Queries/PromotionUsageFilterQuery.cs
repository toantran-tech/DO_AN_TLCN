using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Queries;

public class PromotionUsageFilterQuery : BaseFilterQuery
{
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public bool? IsReverted { get; set; }
}
