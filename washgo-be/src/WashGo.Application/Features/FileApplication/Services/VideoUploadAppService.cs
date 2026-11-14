using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Validation;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Application.Contracts.Features.FileContracts.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Features.FileApplication.Services
{
    /// <summary>Video minh chứng cho đánh giá / khiếu nại (MP4, WEBM, MOV, tối đa 50MB); lưu cùng thư mục ảnh của người dùng.</summary>
    public class VideoUploadAppService : WashGoAppService, IVideoUploadAppService
    {
        private const string SizeMessage = "Video phải có dung lượng từ 1 byte đến 50MB.";

        // ABP's recursive parameter validation would probe Stream properties and throw; inputs are checked below.
        [DisableValidation]
        public async Task<BaseResponse<UploadedImageDto>> UploadAsync(Stream content, long length)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<UploadedImageDto>("Không xác định được tài khoản.");
            if (length <= 0 || length > UploadStorage.MaxVideoBytes)
                return BadRequest<UploadedImageDto>(SizeMessage);

            // The format is decided by the file signature, never by the client-supplied name or content type.
            var header = new byte[12];
            var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            var (extension, contentType) = DetectVideo(header.AsSpan(0, read));
            if (extension == null)
                return BadRequest<UploadedImageDto>("Chỉ chấp nhận video MP4, WEBM hoặc MOV.");

            var owner = CurrentUser.Id.Value.ToString("N");
            var fileName = $"{Guid.NewGuid():N}.{extension}";
            var storage = LazyServiceProvider.LazyGetRequiredService<ObjectStorage>();
            try
            {
                await using var output = await UploadBuffer.CreateAsync(header.AsMemory(0, read), content, UploadStorage.MaxVideoBytes);
                // Persist the TTL registry first so a failed/ambiguous cloud upload is still tracked for cleanup.
                await LazyServiceProvider.LazyGetRequiredService<UploadedAssetRegistry>().RegisterAsync(CurrentUser.Id!.Value, fileName);
                await storage.PutAsync($"{owner}/{fileName}", output, contentType!);
                return Created(new UploadedImageDto
                {
                    Url = $"{UploadStorage.RequestPath}/{owner}/{fileName}",
                    Size = output.Length,
                    ContentType = contentType!
                }, "Upload video thành công.");
            }
            catch (StorageUnavailableException) { throw; }
            catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException)
            {
                return BadRequest<UploadedImageDto>("Không thể upload video. Vui lòng thử lại.");
            }
        }

        private static (string? Extension, string? ContentType) DetectVideo(ReadOnlySpan<byte> h)
        {
            // ISO base media (MP4/MOV): box size, then "ftyp" and the major brand.
            if (h.Length >= 12 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p')
                return h[8] == 'q' && h[9] == 't' ? ("mov", "video/quicktime") : ("mp4", "video/mp4");
            // Matroska/WebM: EBML header.
            if (h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3)
                return ("webm", "video/webm");
            return (null, null);
        }
    }
}
