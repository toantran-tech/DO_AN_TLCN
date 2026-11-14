using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Application.Contracts.Features.FileContracts.Results;
namespace WashGo.Application.Contracts.Features.CustomerContracts.Services;
public interface ICustomerReviewAppService : IApplicationService
{
    Task<BaseResponse<CustomerReviewPageDto>> GetAsync(int page, int take, int? stars);
    Task<BaseResponse<CustomerReviewSummaryDto>> SummaryAsync();
    Task<BaseResponse<PrivateDocumentDownloadDto>> GetImageAsync(Guid publicId, int index);
}
