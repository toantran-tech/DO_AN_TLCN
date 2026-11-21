namespace WashGo.Application.Contracts.Features.FeedbackContracts.Commands;

/// <summary>Admin confirms review of the original comment and media before releasing them to the Merchant.</summary>
public class PublishRatingCommand
{
    /// <summary>
    /// Duyệt và công khai: either flag publishes for Customers and anonymous visitors together (one public scope).
    /// Both false = content approval for the Merchant only (kept for older clients).
    /// </summary>
    public bool PublishForCustomers { get; set; }
    /// <summary>Same meaning as <see cref="PublishForCustomers"/>; sending both is the recommended form.</summary>
    public bool PublishForGuests { get; set; }
    public string? ExpectedVersion { get; set; }
    public Guid? OperationId { get; set; }
    public string? Reason { get; set; }
}
