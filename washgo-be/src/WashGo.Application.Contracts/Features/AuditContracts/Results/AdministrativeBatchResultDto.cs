namespace WashGo.Application.Contracts.Features.AuditContracts.Results;

public class AdministrativeBatchResultDto
{
    public Guid OperationId { get; set; }
    public string State { get; set; } = "Completed";
    public List<AdministrativeBatchItemResultDto> Items { get; set; } = [];
    /// <summary>Đếm theo outcome. Succeeded = Changed + Unchanged (đã ở trạng thái đích cũng là thành công nhưng không phải "vừa đổi").</summary>
    public AdministrativeBatchTotalsDto Totals { get; set; } = new();
}
public class AdministrativeBatchTotalsDto
{
    public int Changed { get; set; }
    public int Unchanged { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Succeeded { get; set; }
}
public class AdministrativeBatchItemResultDto
{
    public Guid Id { get; set; }
    public bool Succeeded { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    /// <summary>Changed (đã đổi), Unchanged (vốn đã đúng đích), Skipped (không còn tồn tại/không áp dụng: 404), Failed (bị từ chối: 400/403/409...).</summary>
    public string Outcome { get; set; } = "Failed";
    /// <summary>Mã lý do ổn định khi Unchanged/Skipped/Failed (ví dụ AlreadyInTargetState, NotFound, VersionMismatch, InUse).</summary>
    public string? ReasonCode { get; set; }
}
