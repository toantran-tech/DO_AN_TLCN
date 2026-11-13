namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperTaskSummaryDto
{
    public int StorageTransferPending { get; set; }
    public int StorageReturnPending { get; set; }
    public int TotalPending { get; set; }
    public int CollectPending { get; set; }
    public int ReturnPending { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ServerNow => GeneratedAt;
    public string? Keyword { get; set; }
    public long AssignedFrom { get; set; }
    public long AssignedTo { get; set; }
    public string FilterBasis { get; set; } = "CurrentPending;KeywordAndAssignedDate;TaskTypeAndStatusIgnored";
}
