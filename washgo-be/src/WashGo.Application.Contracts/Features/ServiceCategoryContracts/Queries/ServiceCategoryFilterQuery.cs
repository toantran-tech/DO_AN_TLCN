namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Queries;
public class ServiceCategoryFilterQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int Take { get; set; } = 20;
    public string SortBy { get; set; } = "sortOrder";
    public bool IsDescending { get; set; }
}
