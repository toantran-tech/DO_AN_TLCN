using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
namespace WashGo.Application.Features.StorageApplication.Services;
public class StorageQuoteCodec(IConfiguration configuration) : ITransientDependency
{
    public sealed record Payload(Guid OrderId, Guid OwnerId, Guid LockerId, Guid? CycleId, decimal FeeTotal, long Expires);
    private byte[] Key => Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT signing key required."));
    public string Write(Payload payload)
    {
        var data = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
        return data + "." + Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(data)));
    }
    public Payload? Read(string token, Guid orderId, Guid owner, DateTime now)
    {
        if (token.Length > 2000) return null;
        try
        {
            var parts = token.Split('.'); if (parts.Length != 2) return null;
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[1]), HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(parts[0])))) return null;
            var value = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(parts[0]));
            return value != null && value.OrderId == orderId && value.OwnerId == owner && value.FeeTotal >= 0 && value.Expires > new DateTimeOffset(now).ToUnixTimeSeconds() ? value : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException) { return null; }
    }
}
