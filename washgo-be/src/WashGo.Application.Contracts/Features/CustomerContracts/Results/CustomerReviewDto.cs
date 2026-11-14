namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;
public class CustomerReviewDto
{
    public Guid Id { get; set; }
    public int OverallStars { get; set; }
    public string? Comment { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public DateTime PublishedAt { get; set; }
}
