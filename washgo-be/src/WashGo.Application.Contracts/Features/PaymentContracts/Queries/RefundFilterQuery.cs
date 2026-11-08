using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Queries;

public class RefundFilterQuery : BaseFilterQuery
{
    public RefundRequestStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? MerchantId { get; set; }
    public string DateBasis { get; set; } = "created";
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}
