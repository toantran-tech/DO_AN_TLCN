using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Services
{
    public interface IIncidentAppService : IApplicationService
    {
        Task<BaseResponse<IncidentResultDto>> CreateAsync(CreateIncidentCommand command);
        Task<PaginatedResult<IncidentResultDto>> GetMineAsync(int page, int take);
        Task<PaginatedResult<IncidentResultDto>> GetListAsync(IncidentFilterQuery query);
        Task<BaseResponse<IncidentResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<IncidentResultDto>> ResolveAsync(Guid id, ResolveIncidentCommand command);
        Task<BaseResponse<IncidentResultDto>> RejectAsync(Guid id, RejectIncidentCommand command);
        Task<BaseResponse<IncidentResultDto>> ConfirmCompensationAsync(Guid id, ConfirmCompensationCommand command);
    }
}
