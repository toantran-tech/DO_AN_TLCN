using System.Net;
using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Features.MerchantApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Audit;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using System.Globalization;
using System.Security.Cryptography;

namespace WashGo.Application.Features.AuditApplication.Services;

/// <summary>Operation lock must precede the resource lock, inside an explicit transactional UoW.</summary>
public class AdministrativeWriteGuard(IRepository<AdministrativeOperation, Guid> operations, IWashOrderRepository locks) : ITransientDependency
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task LockAsync(string resource) => locks.AcquireLockAsync(resource);
    public static string Version(object snapshot)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory)) WriteCanonical(writer, JsonSerializer.SerializeToElement(snapshot, Json));
        return Convert.ToHexString(SHA256.HashData(memory.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); WriteCanonical(writer, p.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            case JsonValueKind.Number when element.TryGetDecimal(out var number):
                writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture)); break;
            default: element.WriteTo(writer); break;
        }
    }
    public static BaseResponse<T> Error<T>(HttpStatusCode status, string code, string message, T? data = default) =>
        MerchantWriteGuard.Error(status, code, message, data);
    public async Task<BaseResponse<T>?> ReplayAsync<T>(Guid? key, Guid actor, string module, string action, string resource, object payload)
    {
        if (!key.HasValue) return null;
        if (key == Guid.Empty) return Error<T>(HttpStatusCode.BadRequest, "InvalidOperationId", "operationId không hợp lệ.");
        await LockAsync($"admin-operation:{key}");
        var old = await operations.FindAsync(key.Value);
        if (old == null) return null;
        if (old.ActorId != actor || old.Module != module || old.Action != action || old.ResourceId != resource || old.PayloadHash != Version(payload))
            return Error<T>(HttpStatusCode.Conflict, "OperationIdReused", "operationId đã được dùng cho yêu cầu khác.");
        return JsonSerializer.Deserialize<BaseResponse<T>>(old.ResponseJson, Json) ?? throw new InvalidOperationException("Invalid stored operation result.");
    }
    public async Task RecordAsync<T>(Guid? key, Guid actor, string module, string action, string resource, object payload, BaseResponse<T> response)
    {
        if (!key.HasValue) return;
        await operations.InsertAsync(new AdministrativeOperation(key.Value)
        {
            ActorId = actor, Module = module, Action = action, ResourceId = resource, PayloadHash = Version(payload),
            ResponseJson = JsonSerializer.Serialize(response, Json), CompletedAt = DateTime.UtcNow
        }, autoSave: true);
    }
    public Task<AdministrativeOperation?> FindAsync(Guid key, Guid actor, string module) =>
        operations.FirstOrDefaultAsync(x => x.Id == key && x.ActorId == actor && x.Module == module);
}
