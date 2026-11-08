using System.Text.Json;

namespace WashGo.Application.Contracts.Features.AuditContracts.Results;

public class AdministrativeOperationDto
{
    public Guid OperationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public string State { get; set; } = "Completed";
    public JsonElement Result { get; set; }
}
