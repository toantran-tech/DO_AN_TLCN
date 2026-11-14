namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;
public class CustomerReviewSummaryDto
{
    public int Total { get; set; }
    public decimal? AverageStars { get; set; }
    public List<CustomerReviewDistributionDto> Distribution { get; set; } = [];
}
