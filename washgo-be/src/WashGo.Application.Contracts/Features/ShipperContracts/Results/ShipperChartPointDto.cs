namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperChartPointDto
{
    public DateTime BucketStart { get; set; }
    public int CollectCount { get; set; }
    public int ReturnCount { get; set; }
}
