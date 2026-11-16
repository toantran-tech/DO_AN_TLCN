namespace WashGo.Application.Contracts.Features.NotificationContracts.Results;
public class NotificationDeliverySummaryDto
{
    public int Pending { get; set; }
    public int Sending { get; set; }
    public int DeadLetter { get; set; }
    public DateTime? OldestPendingAt { get; set; }
    public DateTime ServerNow { get; set; }
}
