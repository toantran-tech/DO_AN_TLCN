using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.Application.Features.AuthApplication.Templates;
using WashGo.Application.Features.FileApplication.Services;

namespace WashGo.HttpApi.Host.Endpoints;

public static class HostedAssetEndpoints
{
    private static readonly Regex FileName = new(@"^[0-9a-f]{32}\.(jpg|png|webp|mp4|webm|mov)\z");

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }))
            .AllowAnonymous().ExcludeFromDescription().WithSummary("Kiểm tra tiến trình API đang chạy.");
        app.MapGet("/health/ready", async (WashGoDbContext database, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                return await database.Database.CanConnectAsync(context.RequestAborted)
                    ? Results.Json(new { status = "ready" })
                    : Results.Json(new { status = "unavailable" }, statusCode: 503);
            }
            catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
            { return Results.Json(new { status = "unavailable" }, statusCode: 503); }
        }).AllowAnonymous().ExcludeFromDescription().WithSummary("Kiểm tra API kết nối được database; không trả thông tin kết nối.");

        app.MapGet("/branding/email-logo.png", () => Results.Stream(
            typeof(AuthEmailTemplates).Assembly.GetManifestResourceStream("WashGo.EmailAssets.logo.png")!, "image/png"))
            .AllowAnonymous().ExcludeFromDescription().WithSummary("Logo PNG dùng trong email WashGo.");

        // Preserve existing media URLs. Private documents are never mapped by this route.
        app.MapGet("/uploads/{owner}/{file}", async (string owner, string file, HttpContext context, ObjectStorage storage) =>
        {
            if (!storage.IsCloud || !Guid.TryParseExact(owner, "N", out _) || owner != owner.ToLowerInvariant() || !FileName.IsMatch(file))
            { context.Response.StatusCode = 404; return; }
            var extension = Path.GetExtension(file);
            context.Response.ContentType = extension switch
            {
                ".jpg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
                ".mp4" => "video/mp4", ".webm" => "video/webm", _ => "video/quicktime"
            };
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                var maximum = extension is ".jpg" or ".png" or ".webp" ? UploadStorage.MaxImageBytes : UploadStorage.MaxVideoBytes;
                if (!await storage.CopyUploadToAsync(owner + "/" + file, context.Response.Body, maximum, context.RequestAborted))
                    context.Response.StatusCode = 404;
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException)
            {
                if (context.Response.HasStarted) context.Abort();
                else context.Response.StatusCode = 503;
            }
        }).AllowAnonymous().ExcludeFromDescription().WithSummary("Đọc ảnh/video qua storage; giữ URL upload hiện hành.");
    }
}
