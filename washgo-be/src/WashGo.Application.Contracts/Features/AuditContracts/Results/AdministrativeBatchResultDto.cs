namespace WashGo.Application.Contracts.Features.AuditContracts.Results;

public class AdministrativeBatchResultDto
{
    public Guid OperationId { get; set; }
    public string State { get; set; } = "Completed";
    public List<AdministrativeBatchItemResultDto> Items { get; set; } = [];
}
public class AdministrativeBatchItemResultDto
{
    public Guid Id { get; set; }
    public bool Succeeded { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
}
