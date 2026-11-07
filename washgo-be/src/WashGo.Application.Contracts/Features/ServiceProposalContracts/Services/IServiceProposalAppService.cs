using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Results;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ServiceProposalContracts.Services
{
    public interface IServiceProposalAppService : IApplicationService
    {
        Task<PaginatedResult<WashServiceResultDto>> GetMyServicesAsync(WashServiceFilterQuery query);
        Task<BaseResponse<WashServiceResultDto>> GetMyServiceAsync(Guid id);
        Task<BaseResponse<MerchantServiceSummaryDto>> GetSummaryAsync();
        Task<BaseResponse<ServiceProposalDto>> ProposeCreateAsync(ServiceProposalCommand command);
        Task<BaseResponse<ServiceProposalDto>> ProposeUpdateAsync(Guid serviceId, ServiceProposalCommand command);
        Task<PaginatedResult<ServiceProposalDto>> GetListAsync(ServiceProposalFilterQuery query);
        Task<BaseResponse<ServiceProposalDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<ServiceProposalDto>> ApproveAsync(Guid id);
        Task<BaseResponse<ServiceProposalDto>> RejectAsync(Guid id, RejectServiceProposalCommand command);
    }
}
