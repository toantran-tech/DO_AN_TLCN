using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AuditContracts.Services;

public interface IAdministrativeOperationAppService : IApplicationService
{
    Task<BaseResponse<AdministrativeOperationDto>> GetAsync(string module, Guid id);
}
