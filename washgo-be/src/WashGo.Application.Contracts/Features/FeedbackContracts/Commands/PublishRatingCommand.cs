namespace WashGo.Application.Contracts.Features.FeedbackContracts.Commands;

/// <summary>Admin confirms review of the original comment and media before releasing them to the Merchant.</summary>
public class PublishRatingCommand
{
    /// <summary>Explicit approval for Customer testimonials, including comment/media PII review. False preserves Merchant-only publishing.</summary>
    public bool PublishForCustomers { get; set; }
    public string? ExpectedVersion { get; set; }
    public Guid? OperationId { get; set; }
    public string? Reason { get; set; }
}
