using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace WashGo.Application.Features.FileApplication.Services;

/// <summary>Stable object keys; cloud credentials and document URLs never leave the backend.</summary>
public sealed class ObjectStorage(IConfiguration configuration, IHttpClientFactory clients) : ITransientDependency
{
    public const string HttpClientName = "WashGoStorage";
    private static readonly Regex UploadKey = new(@"^[0-9a-f]{32}/[0-9a-f]{32}\.(jpg|png|webp|mp4|webm|mov)\z");
    private static readonly Regex DocumentKey = new(@"^[0-9a-f]{32}\.(pdf|doc|docx)\z");
    public bool IsCloud => Provider(configuration) == "Supabase";

    public static string Provider(IConfiguration config)
    {
        var value = config["Storage:Provider"] ?? "Local";
        if (value.Equals("Local", StringComparison.OrdinalIgnoreCase)) return "Local";
        if (value.Equals("Supabase", StringComparison.OrdinalIgnoreCase)) return "Supabase";
        throw new InvalidOperationException("Storage:Provider must be Local or Supabase.");
    }

    private string Bucket(bool documents)
    {
        var uploads = configuration["Storage:Supabase:UploadsBucket"] ?? "washgo-uploads";
        var privateFiles = configuration["Storage:Supabase:DocumentsBucket"] ?? "washgo-documents";
        if (uploads == privateFiles || !Regex.IsMatch(uploads, @"^[a-z0-9][a-z0-9-]{0,62}$") ||
            !Regex.IsMatch(privateFiles, @"^[a-z0-9][a-z0-9-]{0,62}$"))
            throw new InvalidOperationException("Configure two different Supabase bucket names.");
        return documents ? privateFiles : uploads;
    }

    private static void ValidateKey(string key, bool documents)
    {
        if (!(documents ? DocumentKey : UploadKey).IsMatch(key))
            throw new ArgumentException("Invalid storage object key.", nameof(key));
    }

    private string LocalPath(string key, bool documents)
    {
        ValidateKey(key, documents);
        var root = documents ? Path.GetFullPath(configuration["Storage:PrivateDocumentPath"] ??
            Path.Combine(AppContext.BaseDirectory, "private-documents")) : UploadStorage.GetRoot(configuration);
        var publicRoot = UploadStorage.GetRoot(configuration).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (documents && (root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Private documents must not be stored below public uploads.");
        return Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
    }

    private HttpRequestMessage Request(HttpMethod method, string route)
    {
        var url = configuration["Storage:Supabase:Url"];
        var key = configuration["Storage:Supabase:ServiceRoleKey"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.Port != 443 ||
            string.IsNullOrWhiteSpace(key))
            throw new StorageUnavailableException("Configure Storage:Supabase:Url (project HTTPS URL) and ServiceRoleKey.");
        var request = new HttpRequestMessage(method, new Uri(uri, "storage/v1/" + route));
        request.Headers.Add("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        try { return await clients.CreateClient(HttpClientName).SendAsync(request, completion, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { throw new StorageUnavailableException("Storage provider is unavailable."); }
    }

    // Check the bucket before accessing objects: a public bucket would bypass document authorization.
    private async Task CheckBucketAsync(string bucket, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, "bucket/" + bucket);
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        var info = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (info.ValueKind != JsonValueKind.Object || !info.TryGetProperty("public", out var isPublic) || isPublic.ValueKind != JsonValueKind.False)
            throw new StorageUnavailableException("Storage bucket must exist and be private.");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new StorageUnavailableException($"Storage request failed (HTTP {(int)response.StatusCode}).");
    }

    public async Task<bool> ExistsAsync(string key, bool documents = false, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, documents);
        if (!IsCloud) return File.Exists(LocalPath(key, documents));
        var bucket = Bucket(documents);
        await CheckBucketAsync(bucket, cancellationToken);
        using var request = Request(HttpMethod.Head, $"object/authenticated/{bucket}/{key}");
        using var response = await SendAsync(request, cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        EnsureSuccess(response);
        return true;
    }

    public async Task PutAsync(string key, Stream content, string contentType, bool documents = false, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, documents);
        if (!IsCloud)
        {
            var path = LocalPath(key, documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var created = false;
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                created = true;
                await content.CopyToAsync(output, cancellationToken);
            }
            catch { if (created) File.Delete(path); throw; }
            return;
        }
        var bucket = Bucket(documents);
        await CheckBucketAsync(bucket, cancellationToken);
        using var request = Request(HttpMethod.Post, $"object/{bucket}/{key}");
        request.Headers.Add("x-upsert", "false");
        // The caller owns the stream; this content does not dispose it.
        request.Content = new BorrowedStreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    public async Task<byte[]?> ReadAsync(string key, long maxBytes, bool documents = false, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, documents);
        if (!IsCloud)
        {
            var path = LocalPath(key, documents);
            if (!File.Exists(path)) return null;
            await using var file = File.OpenRead(path);
            return await ReadLimitedAsync(file, maxBytes, cancellationToken);
        }
        var bucket = Bucket(documents);
        await CheckBucketAsync(bucket, cancellationToken);
        using var request = Request(HttpMethod.Get, $"object/authenticated/{bucket}/{key}");
        using var response = await SendAsync(request, cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        EnsureSuccess(response);
        if (response.Content.Headers.ContentLength > maxBytes) throw new IOException("Stored object exceeds the size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ReadLimitedAsync(stream, maxBytes, cancellationToken);
    }

    public async Task DeleteAsync(string key, bool documents = false, CancellationToken cancellationToken = default)
    {
        ValidateKey(key, documents);
        if (!IsCloud) { File.Delete(LocalPath(key, documents)); return; }
        var bucket = Bucket(documents);
        await CheckBucketAsync(bucket, cancellationToken);
        using var request = Request(HttpMethod.Delete, "object/" + bucket);
        request.Content = JsonContent.Create(new { prefixes = new[] { key } });
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    public async Task<bool> CopyUploadToAsync(string key, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        ValidateKey(key, documents: false);
        if (!IsCloud)
        {
            var path = LocalPath(key, documents: false);
            if (!File.Exists(path)) return false;
            await using var input = File.OpenRead(path);
            await CopyLimitedAsync(input, destination, maxBytes, cancellationToken);
            return true;
        }
        var bucket = Bucket(documents: false);
        await CheckBucketAsync(bucket, cancellationToken);
        using var request = Request(HttpMethod.Get, $"object/authenticated/{bucket}/{key}");
        using var response = await SendAsync(request, cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        EnsureSuccess(response);
        if (response.Content.Headers.ContentLength > maxBytes) throw new IOException("Stored object exceeds the size limit.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await CopyLimitedAsync(source, destination, maxBytes, cancellationToken);
        return true;
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += count;
            if (total > maxBytes) throw new IOException("Stored object exceeds the size limit.");
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }

    public static async Task<byte[]> ReadLimitedAsync(Stream source, long maxBytes, CancellationToken cancellationToken = default)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + count > maxBytes) throw new IOException("Stored object exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        return output.ToArray();
    }

    private sealed class BorrowedStreamContent(Stream source) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => source.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length)
        { length = source.CanSeek ? source.Length - source.Position : 0; return source.CanSeek; }
    }
}
