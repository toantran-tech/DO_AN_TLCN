namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Results;
public class ServiceCategoryDto
{
    public Guid Id { get; set; }
    public int? LegacyServiceType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? Version { get; set; }
    public int ServiceCount { get; set; }
    public int PendingProposalCount { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanChangeStatus { get; set; }
    public string? DeleteReasonCode { get; set; }
}
