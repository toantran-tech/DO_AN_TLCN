using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Queries;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CustomerServiceFilterQuery : BaseFilterQuery
{
    public CustomerServiceFilterQuery()
    {
        Page = 1;
        Take = 20;
    }

    public WashServiceType[] ServiceTypes { get; set; } = [];
    public ServicePriceRangeQuery[] PriceRanges { get; set; } = [];
}
