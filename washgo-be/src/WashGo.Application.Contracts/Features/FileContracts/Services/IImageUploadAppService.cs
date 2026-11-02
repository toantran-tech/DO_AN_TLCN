using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FileContracts.Services
{
    public interface IImageUploadAppService : IApplicationService
    {
        Task<BaseResponse<UploadedImageDto>> UploadAsync(Stream content, long length);
    }
}
