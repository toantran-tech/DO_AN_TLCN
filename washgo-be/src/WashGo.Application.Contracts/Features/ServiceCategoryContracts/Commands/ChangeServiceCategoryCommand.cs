using System.Text.Json.Serialization;
namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
public class ChangeServiceCategoryCommand
{
    public Guid OperationId { get; set; }
    public string? ExpectedVersion { get; set; }
    [JsonRequired] public bool IsActive { get; set; }
    public string? Reason { get; set; }
}
