using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Features.AuthApplication.Templates;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services;

public sealed class BrevoEmailClient(IHttpClientFactory clients, IConfiguration configuration,
    ILogger<BrevoEmailClient> logger) : ITransientDependency
{
    public const string HttpClientName = "WashGoBrevo";

    public async Task<CredentialDeliveryStatus> SendAsync(string email, string subject, string body, bool isHtml)
    {
        var key = configuration["Email:Brevo:ApiKey"];
        var sender = configuration["Email:SenderAddress"];
        var name = configuration["Email:SenderName"] ?? "WashGo";
        var publicUrl = configuration["App:PublicBaseUrl"] ?? configuration["RENDER_EXTERNAL_URL"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender) ||
            isHtml && (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0))
        {
            logger.LogError("Brevo requires Email:Brevo:ApiKey, verified Email:SenderAddress and HTTPS App:PublicBaseUrl (or RENDER_EXTERNAL_URL).");
            return CredentialDeliveryStatus.Failed;
        }
        // Keep the existing HTML templates. SMTP embeds CID; HTTPS email uses our public, immutable PNG.
        if (isHtml) body = body.Replace($"cid:{AuthEmailTemplates.LogoContentId}",
            publicUrl!.TrimEnd('/') + "/branding/email-logo.png", StringComparison.Ordinal);
        var payload = new Dictionary<string, object>
        {
            ["sender"] = new { email = sender, name },
            ["to"] = new[] { new { email } },
            ["subject"] = subject,
            [isHtml ? "htmlContent" : "textContent"] = body
        };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", key);
            request.Content = JsonContent.Create(payload);
            // Do not retry: a lost acknowledgement can follow provider acceptance.
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                return result.ValueKind == JsonValueKind.Object && result.TryGetProperty("messageId", out var id) &&
                    id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())
                    ? CredentialDeliveryStatus.Sent : CredentialDeliveryStatus.Unknown;
            }
            logger.LogWarning("Brevo rejected email: HTTP {StatusCode}.", (int)response.StatusCode);
            return response.StatusCode is HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500
                ? CredentialDeliveryStatus.Unknown : CredentialDeliveryStatus.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Never log provider bodies, recipient addresses, OTPs, temporary passwords or API keys.
            logger.LogWarning("Brevo delivery acknowledgement unavailable: {ErrorType}.", ex.GetType().Name);
            return CredentialDeliveryStatus.Unknown;
        }
    }
}
