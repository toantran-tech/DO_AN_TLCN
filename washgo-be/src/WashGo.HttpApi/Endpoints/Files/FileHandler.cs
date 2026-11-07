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
            group.MapPost("/images", async (IFormFile file, IImageUploadAppService svc) =>
            {
                await using var stream = file.OpenReadStream();
                return (await svc.UploadAsync(stream, file.Length)).CustomResult();
            })
                .DisableAntiforgery()
                .WithName("Upload ảnh minh chứng")
                .WithSummary("Upload một ảnh JPG/PNG/WEBP tối đa 5MB (form field 'file'); trả URL dùng cho đánh giá hoặc báo cáo sự cố.")
                .WithOpenApi();

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
                .WithOpenApi();
        }
    }
}
