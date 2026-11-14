using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.FileContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Files
{
    public class FileHandler : IEndpointBase
    {
        private const string _endpoint = "/files";
        // Video plus multipart overhead.
        private const long VideoRequestLimit = 52 * 1024 * 1024;

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("File - Upload ảnh")
            .RequireAuthorization();

            // Bearer-token API, so no cookie-based antiforgery token is involved.
            group.MapPost("/media/cleanup", async (IPrivateDocumentAppService svc) => (await svc.CleanupMediaAsync()).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Dọn tối đa 100 ảnh/video upload mới quá 24h chưa gắn bản ghi; giữ mọi file đã từng tham chiếu, không đụng dữ liệu legacy chưa có registry.").WithOpenApi();
            var documents = group.MapGroup("/documents").RequireAuthorization("MerchantOrAdmin");
            documents.MapPost("/", async (HttpContext context, IPrivateDocumentAppService svc) =>
            {
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                    limit.MaxRequestBodySize = 11 * 1024 * 1024;
                if (!context.Request.HasFormContentType) return Results.BadRequest(new { message = "Cần multipart/form-data." });
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var file = form.Files.GetFile("file");
                if (file == null) return Results.BadRequest(new { message = "Thiếu file." });
                Guid? merchantId = null;
                if (!string.IsNullOrWhiteSpace(form["merchantId"]) && !Guid.TryParse(form["merchantId"], out _))
                    return Results.BadRequest(new { message = "merchantId không hợp lệ." });
                if (Guid.TryParse(form["merchantId"], out var parsed)) merchantId = parsed;
                await using var stream = file.OpenReadStream();
                return (await svc.UploadAsync(stream, file.Length, file.FileName, form["kind"].ToString(), merchantId)).CustomResult();
            }).DisableAntiforgery().Accepts<IFormFile>("multipart/form-data")
                .WithSummary("Upload giấy tờ PDF/DOC/DOCX tối đa 10 MB: file, kind=BusinessLicense|Contract|Sla, merchantId tùy chọn. Merchant chỉ upload giấy phép của mình; bản nháp hết hạn sau 24 giờ.").WithOpenApi(UploadOpenApi.Documents);
            documents.MapGet("/{id:guid}", async (Guid id, IPrivateDocumentAppService svc) => (await svc.GetAsync(id)).CustomResult())
                .WithSummary("Xem metadata giấy tờ được phép truy cập.").WithOpenApi();
            documents.MapGet("/{id:guid}/download", async (Guid id, HttpContext context, IPrivateDocumentAppService svc) =>
            {
                var response = await svc.DownloadAsync(id);
                if (!response.Succeeded || response.Data == null) return response.CustomResult();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return Results.File(response.Data.Bytes, response.Data.ContentType, response.Data.FileName);
            }).WithSummary("Tải giấy tờ riêng tư; bắt buộc JWT và quyền Admin/đúng Merchant.").WithOpenApi();
            documents.MapDelete("/{id:guid}", async (Guid id, IPrivateDocumentAppService svc) => (await svc.DeleteDraftAsync(id)).CustomResult())
                .WithSummary("Xóa bản nháp do mình upload; không xóa giấy tờ đã được hồ sơ/lịch sử tham chiếu.").WithOpenApi();
            documents.MapPost("/cleanup", async (IPrivateDocumentAppService svc) => (await svc.CleanupExpiredAsync()).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Admin dọn tối đa 100 bản nháp quá hạn; worker cũng chạy định kỳ.").WithOpenApi();

            group.MapPost("/images", async (HttpContext context, IImageUploadAppService svc) =>
            {
                if (!context.Request.HasFormContentType)
                    return Results.BadRequest(new { message = "Cần multipart/form-data với file ảnh trong trường 'file'." });
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var file = form.Files.GetFile("file");
                if (file == null)
                    return Results.BadRequest(new { message = "Thiếu file ảnh. Vui lòng chọn ảnh trong trường 'file'." });
                await using var stream = file.OpenReadStream();
                return (await svc.UploadAsync(stream, file.Length)).CustomResult();
            })
                .DisableAntiforgery()
                .Accepts<IFormFile>("multipart/form-data")
                .WithName("Upload ảnh minh chứng")
                .WithSummary("Upload một ảnh JPG/PNG/WEBP tối đa 5MB (form field 'file'); trả URL dùng cho đánh giá hoặc báo cáo sự cố.")
                .WithOpenApi(UploadOpenApi.Media);

            // The form is read by hand: Kestrel's default 30MB body limit must be raised before the body is read.
            group.MapPost("/videos", async (HttpContext context, IVideoUploadAppService svc) =>
            {
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                    limit.MaxRequestBodySize = VideoRequestLimit;
                if (!context.Request.HasFormContentType)
                    return Results.BadRequest(new { message = "Vui lòng gửi video trong form field 'file'." });
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var file = form.Files.GetFile("file");
                if (file == null)
                    return Results.BadRequest(new { message = "Vui lòng gửi video trong form field 'file'." });
                await using var stream = file.OpenReadStream();
                return (await svc.UploadAsync(stream, file.Length)).CustomResult();
            })
                .DisableAntiforgery()
                .Accepts<IFormFile>("multipart/form-data")
                .WithName("Upload video minh chứng")
                .WithSummary("Upload một video MP4/WEBM/MOV tối đa 50MB (form field 'file'); trả URL dùng trong videoUrls của đánh giá hoặc báo cáo sự cố (tối đa 2 video).")
                .WithOpenApi(UploadOpenApi.Media);
        }
    }
}
