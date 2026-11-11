namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperTaskCredentialDto
{
    public Guid TaskId { get; set; }
    public Guid AssignmentVersion => TaskId;
    public long OrderRevision { get; set; }
    public string PinCode { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public DateTime? LockoutUntil { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTime ServerNow { get; set; }
}
