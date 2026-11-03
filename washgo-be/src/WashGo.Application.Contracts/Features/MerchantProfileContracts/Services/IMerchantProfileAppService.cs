using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

using WashGo.Application.Contracts.Features.MerchantProfileContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Results;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts.Services
{
    public interface IMerchantProfileAppService : IApplicationService
    {
        Task<BaseResponse<MerchantProfileDto>> GetMyProfileAsync();
        Task<BaseResponse<MerchantProfileRequestDto>> RequestUpdateAsync(MerchantProfileUpdateCommand command);
        Task<PaginatedResult<MerchantProfileRequestDto>> GetRequestsAsync(MerchantProfileRequestFilterQuery query);
        Task<BaseResponse<MerchantProfileRequestDto>> GetRequestAsync(Guid id);
        Task<BaseResponse<MerchantProfileRequestDto>> ApproveAsync(Guid id);
        Task<BaseResponse<MerchantProfileRequestDto>> RejectAsync(Guid id, RejectMerchantProfileRequestCommand command);
    }
}
