using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WashGo.Application.Features.AuthApplication.Services;
using WashGo.Application.Features.AuthApplication.Templates;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Domain.Shared.Enums;

internal static class HostedProviderChecks
{
    public static async Task RunAsync()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Email:Brevo:ApiKey"] = "fake-test-key", ["Email:SenderAddress"] = "sender@example.test",
            ["App:PublicBaseUrl"] = "https://washgo.example.test", ["Storage:Provider"] = "Supabase",
            ["Storage:Supabase:Url"] = "https://fixture.supabase.co",
            ["Storage:Supabase:ServiceRoleKey"] = "fake-storage-key"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var handler = new StubHandler();
        var factory = new StubFactory(handler);
        var mail = new BrevoEmailClient(factory, config, NullLogger<BrevoEmailClient>.Instance);
        var html = AuthEmailTemplates.RenderOtpEmail("OTP", "Fixture", "654321");
        var requests = 0;
        handler.Handle = async request =>
        {
            requests++;
            Check(request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri == "https://api.brevo.com/v3/smtp/email", "Brevo API route");
            Check(request.Headers.GetValues("api-key").Single() == "fake-test-key", "Brevo auth header");
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
            var content = body.GetProperty("htmlContent").GetString()!;
            Check(content.Contains("654321") && content.Contains("https://washgo.example.test/branding/email-logo.png") && !content.Contains("cid:"), "Template and HTTPS PNG preserved");
            Check(body.GetProperty("to")[0].GetProperty("email").GetString() == "recipient@example.test", "Email recipient");
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { messageId = "fixture-id" }) };
        };
        Check(await mail.SendAsync("recipient@example.test", "Fixture", html, true) == CredentialDeliveryStatus.Sent, "Provider acceptance");
        handler.Handle = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        Check(await mail.SendAsync("recipient@example.test", "Fixture", html, true) == CredentialDeliveryStatus.Failed, "Rejected key");
        handler.Handle = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Check(await mail.SendAsync("recipient@example.test", "Fixture", html, true) == CredentialDeliveryStatus.Unknown, "Provider 5xx is ambiguous");
        handler.Handle = _ => throw new HttpRequestException("Fixture network error");
        Check(await mail.SendAsync("recipient@example.test", "Fixture", html, true) == CredentialDeliveryStatus.Unknown, "Ambiguous delivery must not retry");
        var before = requests;
        config["Email:Brevo:ApiKey"] = "";
        Check(await mail.SendAsync("recipient@example.test", "Fixture", html, true) == CredentialDeliveryStatus.Failed && requests == before, "Missing key fails without SMTP fallback");
        config["Email:Provider"] = "Brevo";
        var sender = new AuthEmailSender(config, NullLogger<AuthEmailSender>.Instance, mail);
        Check(await sender.SendMerchantCredentialsAsync("recipient@example.test", "Fixture", "fixture-password") == CredentialDeliveryStatus.Failed,
            "Selected Brevo must not fall back to SMTP");

        var storage = new ObjectStorage(config, factory);
        var owner = Guid.NewGuid();
        var uploadKey = $"{owner:N}/{Guid.NewGuid():N}.png";
        var documentKey = $"{Guid.NewGuid():N}.pdf";
        var payload = Encoding.UTF8.GetBytes("bounded fixture bytes");
        var objects = new Dictionary<string, byte[]>();
        var publicBucket = false;
        handler.Handle = async request =>
        {
            requests++;
            Check(request.Headers.Authorization?.Parameter == "fake-storage-key" && request.Headers.GetValues("apikey").Single() == "fake-storage-key", "Storage server auth");
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/storage/v1/bucket/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { @public = publicBucket }) };
            if (request.Method == HttpMethod.Delete)
            {
                var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
                Check(body.GetProperty("prefixes")[0].GetString() == uploadKey, "Exact object deletion");
                objects.Remove("washgo-uploads/" + uploadKey);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
            }
            var key = path.Replace("/storage/v1/object/authenticated/", "").Replace("/storage/v1/object/", "");
            if (request.Method == HttpMethod.Post)
            {
                Check(request.Headers.GetValues("x-upsert").Single() == "false", "Never overwrite an existing object");
                Check(request.Content!.Headers.ContentType?.MediaType == (key.EndsWith(".pdf") ? "application/pdf" : "image/png"), "Stored MIME type");
                objects.Add(key, await request.Content.ReadAsByteArrayAsync());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { Key = key }) };
            }
            Check(path.Contains("/authenticated/"), "Downloads must not use the public object route");
            return objects.TryGetValue(key, out var value)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(value) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        using var upload = new MemoryStream(payload);
        await storage.PutAsync(uploadKey, upload, "image/png");
        Check(upload.CanRead && await storage.ExistsAsync(uploadKey), "Upload stream ownership and existence");
        Check((await storage.ReadAsync(uploadKey, 1024))!.SequenceEqual(payload), "Authenticated media download");
        using var document = new MemoryStream(payload);
        await storage.PutAsync(documentKey, document, "application/pdf", documents: true);
        Check(objects.ContainsKey("washgo-documents/" + documentKey), "Private documents use their own bucket");
        Check((await storage.ReadAsync(documentKey, 1024, documents: true))!.SequenceEqual(payload), "Private document download");
        await ExpectAsync<IOException>(async () => { await storage.ReadAsync(documentKey, 1, documents: true); });
        publicBucket = true;
        await ExpectAsync<IOException>(async () => { await storage.ReadAsync(documentKey, 1024, documents: true); });
        publicBucket = false;
        before = requests;
        await ExpectAsync<ArgumentException>(() => storage.DeleteAsync("../" + documentKey, documents: true));
        Check(requests == before, "Traversal rejected before network access");
        var (error, _) = await UploadStorage.ValidateOwnedImagesAsync(storage, ["/uploads/" + uploadKey], Guid.NewGuid(), 1);
        Check(error != null && requests == before, "Cross-owner evidence rejected before storage access");
        await storage.DeleteAsync(uploadKey);
        Check(!await storage.ExistsAsync(uploadKey), "Deleted media is absent");
        Console.WriteLine("PASS: Brevo template/logo/auth/error contracts; Supabase private buckets, upload/read/delete, byte limit, traversal and owner checks (fake HTTP only).");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task ExpectAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handle { get; set; } = _ => throw new Exception("Unexpected HTTP request");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Handle(request);
    }
    private sealed class StubFactory(StubHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
