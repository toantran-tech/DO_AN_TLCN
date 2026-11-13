using System.Text.Json;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public static class ChatbotJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Read<T>(string? value) => value == null ? default : JsonSerializer.Deserialize<T>(value, Options);
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}
