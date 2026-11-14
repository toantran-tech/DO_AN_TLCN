using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Validation;
using WashGo.Application.Contracts.Features.FileContracts.Results;
using WashGo.Application.Contracts.Features.FileContracts.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Features.FileApplication.Services
{
    public class ImageUploadAppService : WashGoAppService, IImageUploadAppService
    {
        // ABP's recursive parameter validation would probe Stream properties and throw; inputs are checked below.
        [DisableValidation]
        public async Task<BaseResponse<UploadedImageDto>> UploadAsync(Stream content, long length)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<UploadedImageDto>("Không xác định được tài khoản.");
            if (length <= 0 || length > UploadStorage.MaxImageBytes)
                return BadRequest<UploadedImageDto>("Ảnh phải có dung lượng từ 1 byte đến 5MB.");

            // The format is decided by the file signature, never by the client-supplied name or content type.
            var header = new byte[12];
            var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            var (extension, contentType) = DetectImage(header.AsSpan(0, read));
            if (extension == null)
                return BadRequest<UploadedImageDto>("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

            var owner = CurrentUser.Id.Value.ToString("N");
            var fileName = $"{Guid.NewGuid():N}.{extension}";
            var storage = LazyServiceProvider.LazyGetRequiredService<ObjectStorage>();
            try
            {
                await using var output = await UploadBuffer.CreateAsync(header.AsMemory(0, read), content, UploadStorage.MaxImageBytes);
                // Persist the TTL registry first so a failed/ambiguous cloud upload is still tracked for cleanup.
                await LazyServiceProvider.LazyGetRequiredService<UploadedAssetRegistry>().RegisterAsync(CurrentUser.Id.Value, fileName);
                await storage.PutAsync($"{owner}/{fileName}", output, contentType!);
                return Created(new UploadedImageDto
                {
                    Url = $"{UploadStorage.RequestPath}/{owner}/{fileName}",
                    Size = output.Length,
                    ContentType = contentType!
                }, "Upload ảnh thành công.");
            }
            catch (StorageUnavailableException) { throw; }
            catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException)
            {
                return BadRequest<UploadedImageDto>("Không thể upload ảnh. Vui lòng thử lại.");
            }
        }

        private static (string? Extension, string? ContentType) DetectImage(ReadOnlySpan<byte> h)
        {
            if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
                return ("jpg", "image/jpeg");
            if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
                h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
                return ("png", "image/png");
            if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' &&
                h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P')
                return ("webp", "image/webp");
            return (null, null);
        }
    }
}
