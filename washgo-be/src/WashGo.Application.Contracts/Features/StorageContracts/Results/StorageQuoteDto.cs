namespace WashGo.Application.Contracts.Features.StorageContracts.Results;
public class StorageQuoteDto
{
    public Guid OrderId { get; set; }
    public Guid LockerId { get; set; }
    public decimal FeeTotal { get; set; }
    public decimal Outstanding { get; set; }
    public string QuoteToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
