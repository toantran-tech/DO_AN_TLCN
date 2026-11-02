using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.CustomerContracts.Commands;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Services;

public interface ICustomerAppService : IApplicationService
{
    Task<BaseResponse<CustomerProfileDto>> GetMyProfileAsync();
    Task<BaseResponse<CustomerProfileDto>> UpdateMyProfileAsync(UpdateCustomerProfileCommand command);
}
