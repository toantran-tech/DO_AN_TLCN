using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceCategoryContracts.Results;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
namespace WashGo.Application.Contracts.Features.ServiceCategoryContracts.Services;
public interface IServiceCategoryAppService : IApplicationService
{
    Task<PaginatedResult<ServiceCategoryDto>> FilterAsync(ServiceCategoryFilterQuery query);
    Task<PaginatedResult<ServiceCategoryDto>> LookupAsync(string? keyword, int page, int take, bool includeInactive);
    Task<BaseResponse<ServiceCategoryDto>> GetAsync(Guid id);
    Task<BaseResponse<ServiceCategoryDto>> SaveAsync(Guid? id, SaveServiceCategoryCommand command);
    Task<BaseResponse<ServiceCategoryDto>> ChangeAsync(Guid id, ChangeServiceCategoryCommand command, bool delete);
    Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command);
    Task<BaseResponse<AdministrativeBatchResultDto>> GetOperationAsync(Guid id);
}
