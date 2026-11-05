namespace WashGo.Application.Contracts.Features.CustomerContracts.Queries;

public class ServicePriceRangeQuery
{
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
