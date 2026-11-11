using System.Text.Json;

namespace WashGo.Application.Features.FileApplication.Services;

/// <summary>Tolerates legacy JSON stored on resources; missing/broken images never remove a business row.</summary>
public static class StoredImageUrls
{
    public static List<string> Read(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
