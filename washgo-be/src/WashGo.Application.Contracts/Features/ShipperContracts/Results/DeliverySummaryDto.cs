namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class DeliverySummaryDto
{
    public int Total { get; set; }
    public int CollectCount { get; set; }
    public int ReturnCount { get; set; }
    public int CapturedSnapshotCount { get; set; }
    public long FromDate { get; set; }
    public long ToDate { get; set; }
    public string? Keyword { get; set; }
    public Guid? LockerId { get; set; }
    public bool? CancelledReturn { get; set; }
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public DateTime GeneratedAt { get; set; }
    public string FilterBasis { get; set; } = "CompletionActorAndDate;TaskTypeIgnored";
}
