namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;
public class CustomerReviewPageDto
{
    public List<CustomerReviewDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Take { get; set; }
    public bool HasMore => (long)Page * Take < Total;
}
