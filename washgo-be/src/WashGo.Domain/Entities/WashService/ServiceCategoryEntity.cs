using WashGo.Core.Domain.Common;

namespace WashGo.Domain.Entities.WashService;

public class ServiceCategoryEntity : BaseEntity
{
    public ServiceCategoryEntity() { }
    public ServiceCategoryEntity(Guid id) : base(id) { }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public long Revision { get; set; } = 1;
}
