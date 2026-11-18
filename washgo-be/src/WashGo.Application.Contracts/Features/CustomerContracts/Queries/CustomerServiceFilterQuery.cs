using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Queries;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CustomerServiceFilterQuery : BaseFilterQuery
{
        public Guid[]? CategoryIds { get; set; }
    public CustomerServiceFilterQuery()
    {
        Page = 1;
        Take = 20;
    }

    /// <summary>Lọc legacy: tương đương lọc theo các danh mục gốc của những loại này; nên dùng categoryIds.</summary>
    public WashServiceType[] ServiceTypes { get; set; } = [];
    public ServicePriceRangeQuery[] PriceRanges { get; set; } = [];
}
