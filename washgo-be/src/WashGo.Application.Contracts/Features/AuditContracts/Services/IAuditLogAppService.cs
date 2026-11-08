using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.AuditContracts.Services
{
    public interface IAuditLogAppService : IApplicationService
    {
        Task<PaginatedResult<AuditLogDto>> GetListAsync(AuditLogFilterQuery query);
        Task<BaseResponse<AuditLogDto>> GetByIdAsync(Guid id);
        Task<PaginatedResult<AuditLogDto>> GetMyActivitiesAsync(MyActivityQuery query);
    }
}
