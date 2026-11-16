namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
public class SaveServiceCategoryCommand
{
    public Guid OperationId { get; set; }
    public string? ExpectedVersion { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Reason { get; set; }
}
