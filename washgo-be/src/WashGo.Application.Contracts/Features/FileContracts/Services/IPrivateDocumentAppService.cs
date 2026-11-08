using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FileContracts.Services;

public interface IPrivateDocumentAppService : IApplicationService
{
    Task<BaseResponse<PrivateDocumentDto>> UploadAsync(Stream stream, long length, string fileName, string kind, Guid? merchantId);
    Task<BaseResponse<PrivateDocumentDto>> GetAsync(Guid id);
    Task<BaseResponse<PrivateDocumentDownloadDto>> DownloadAsync(Guid id);
    Task<BaseResponse<bool>> DeleteDraftAsync(Guid id);
    Task<BaseResponse<int>> CleanupExpiredAsync();
    Task<BaseResponse<int>> CleanupMediaAsync();
}
