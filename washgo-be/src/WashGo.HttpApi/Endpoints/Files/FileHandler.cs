using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.FileContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Files
{
    public class FileHandler : IEndpointBase
    {
        private const string _endpoint = "/files";

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
        }
    }
}
