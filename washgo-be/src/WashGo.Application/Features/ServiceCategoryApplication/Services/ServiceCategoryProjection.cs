using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ServiceCategoryApplication.Services;
public class ServiceCategoryProjection(IRepository<ServiceCategoryEntity, Guid> categories) : ITransientDependency
{
    public async Task FillAsync(IEnumerable<WashServiceResultDto> services)
    {
        var rows = services.ToList(); var ids = rows.Select(x => x.CategoryId).Distinct().ToList();
        if (ids.Count == 0) return;
        var lookup = (await categories.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);
        foreach (var row in rows)
        {
            var category = lookup.GetValueOrDefault(row.CategoryId);
            row.CategoryCode = category?.Code; row.CategoryName = category?.Name;
            row.CategoryIsActive = category is { IsActive: true, IsDeleted: false };
            row.BlockedReason = category == null || category.IsDeleted ? "CategoryDeleted" : !category.IsActive ? "CategoryInactive" :
                !row.IsActive ? "ServiceInactive" : row.ApprovalStatus != ServiceApprovalStatus.Approved ? "ServiceNotApproved" : row.UnitPrice <= 0 ? "ServicePriceInvalid" : null;
            row.EffectiveIsActive = row.BlockedReason == null;
        }
    }
}
