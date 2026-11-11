namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries;

public class ShipperDashboardQuery
{
    public string Period { get; set; } = "day";
    public long FromDate { get; set; }
    public long ToDate { get; set; }
}
