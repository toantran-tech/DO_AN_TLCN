using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
namespace WashGo.Application.Contracts.Features.StorageContracts.Queries;
public class StorageFilterQuery : BaseFilterQuery
{
    public StorageFilterQuery() { Page = 1; Take = 20; }
    public StorageCycleState? State { get; set; }
}
