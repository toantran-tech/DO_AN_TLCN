using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.PolicyContracts.Commands;
using WashGo.Application.Contracts.Features.PolicyContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.PolicyContracts.Services
{
    public interface IPolicyAppService : IApplicationService
    {
        Task<BaseResponse<CurrentPolicyDto>> GetCurrentAsync();
        Task<BaseResponse<CustomerPolicyDto>> GetForCustomerAsync();
        Task<BaseResponse<CustomerPolicyDto>> AcknowledgeAsync(AcknowledgeTermsCommand command);
        Task<BaseResponse<List<TermsPolicyDto>>> GetVersionsAsync();
        Task<BaseResponse<TermsPolicyDto>> PublishAsync(PublishTermsCommand command);
    }
}
