namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperPerformanceDto
{
    public int Total { get; set; }
    public int OnTimeCount { get; set; }
    public int SlaSampleCount { get; set; }
    public int UnknownSlaCount => Total - SlaSampleCount;
    public int DurationSampleCount { get; set; }
    public int CapturedSnapshotCount { get; set; }
    public double? OnTimeRate => SlaSampleCount == 0 ? null : 100d * OnTimeCount / SlaSampleCount;
    public double? AverageDurationHours { get; set; }
}
