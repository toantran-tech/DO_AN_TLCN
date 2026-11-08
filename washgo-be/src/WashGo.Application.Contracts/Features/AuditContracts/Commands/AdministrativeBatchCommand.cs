namespace WashGo.Application.Contracts.Features.AuditContracts.Commands;

public class AdministrativeBatchCommand
{
    public Guid OperationId { get; set; }
    public List<AdministrativeBatchItem> Items { get; set; } = [];
    /// <summary>Delete hoặc Status. Status dùng giá trị đích, không đảo trạng thái.</summary>
    public string Action { get; set; } = string.Empty;
    public int? Status { get; set; }
    public bool? IsActive { get; set; }
}
public class AdministrativeBatchItem
{
    public Guid Id { get; set; }
    public string ExpectedVersion { get; set; } = string.Empty;
}
