namespace WashGo.Application.Contracts.Features.ShipperContracts.Results;

public class ShipperDashboardDto
{
    public int PendingStorageTransferCount { get; set; }
    public int PendingStorageReturnCount { get; set; }
    public int CompletedStorageTransferCount { get; set; }
    public int CompletedStorageReturnCount { get; set; }
    public List<MyShipperTaskDto> PendingStorage { get; set; } = [];
    public int PendingCollectCount { get; set; }
    public int PendingReturnCount { get; set; }
    public int CompletedCollectCount { get; set; }
    public int CompletedReturnCount { get; set; }
    public int TotalDeliveries { get; set; }
    public string TotalDeliveriesBasis { get; set; } = "LifetimeAssignedShipperCounterIncludingAdminCompletion";
    public string CompletionBasis { get; set; } = "ActualShipperActor";
    public string PendingBasis { get; set; } = "Current";
    public int Status { get; set; }
    public bool IsActive { get; set; }
    public Guid MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ServerNow => GeneratedAt;
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime PreviousFrom { get; set; }
    public DateTime PreviousTo { get; set; }
    public bool IsPartialPeriod { get; set; }
    public ShipperPerformanceDto Performance { get; set; } = new();
    public ShipperPerformanceDto PreviousPerformance { get; set; } = new();
    public List<ShipperChartPointDto> Chart { get; set; } = [];
    public List<MyShipperTaskDto> PendingCollect { get; set; } = [];
    public List<MyShipperTaskDto> PendingReturn { get; set; } = [];
    public List<DeliveryHistoryDto> RecentCompletions { get; set; } = [];
}
