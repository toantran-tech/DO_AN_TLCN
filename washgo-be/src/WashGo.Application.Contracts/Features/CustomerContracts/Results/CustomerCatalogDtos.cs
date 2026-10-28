using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;

public class CustomerServiceCatalogDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WashServiceType ServiceType { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? PricePerKg { get; set; }
    public decimal? PricePerItem { get; set; }
    public int EstimatedDurationHours { get; set; }
    public string? Images { get; set; }
}

public class CustomerLockerCatalogDto
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int AvailableSlotCount { get; set; }
    public string? Description { get; set; }
    public string? Images { get; set; }
    public List<CustomerLockerSlotDto> AvailableSlots { get; set; } = [];
}

public class CustomerLockerSlotDto
{
    public Guid Id { get; set; }
    public int SlotNumber { get; set; }
    public LockerBoxSize Size { get; set; }
}
