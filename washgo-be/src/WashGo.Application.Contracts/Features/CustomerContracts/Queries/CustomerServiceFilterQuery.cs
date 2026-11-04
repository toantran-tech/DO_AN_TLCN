using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Queries;

public class CustomerServiceFilterQuery : BaseFilterQuery
{
    public CustomerServiceFilterQuery()
    {
        Page = 1;
        Take = 20;
    }

    public WashServiceType? ServiceType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
