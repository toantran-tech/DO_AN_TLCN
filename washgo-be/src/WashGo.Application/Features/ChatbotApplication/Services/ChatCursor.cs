using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public class ChatCursor(ChatbotOptions options) : ITransientDependency
{
    public sealed record Position(Guid Owner, string Resource, long Snapshot, long Before, long Expires);
    private byte[] Key => options.SigningKey.Length >= 32
        ? Encoding.UTF8.GetBytes("washgo-chat-v1:" + options.SigningKey)
        : throw new InvalidOperationException("Chatbot signing key must have at least 32 characters.");
    public string Fingerprint(string value) => Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(value)));
    public string Encode(Guid owner, string resource, long snapshot, long before)
    {
        var data = ChatbotJson.Serialize(new Position(owner, resource, snapshot, before, DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds()));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data)) + "." + Fingerprint(data);
    }
    public Position? Decode(string? value, Guid owner, string resource)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 1200) return null;
        try
        {
            var pieces = value.Split('.');
            if (pieces.Length != 2) return null;
            var data = Encoding.UTF8.GetString(Convert.FromBase64String(pieces[0]));
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Fingerprint(data)), Convert.FromHexString(pieces[1]))) return null;
            var p = JsonSerializer.Deserialize<Position>(data, ChatbotJson.Options);
            return p is { Snapshot: >= 0, Before: > 0 } && p.Before <= p.Snapshot && p.Owner == owner &&
                p.Resource == resource && p.Expires >= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? p : null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException) { return null; }
    }
}
