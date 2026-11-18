namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
public class SaveServiceCategoryCommand
{
    public Guid OperationId { get; set; }
    public string? ExpectedVersion { get; set; }
    /// <summary>Không cần gửi: tạo mới thì BE tự cấp DMdddd (mã gửi kèm bị bỏ qua); sửa chỉ chấp nhận đúng mã hiện tại.</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Reason { get; set; }
}
