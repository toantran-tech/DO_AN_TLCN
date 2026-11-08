using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;

namespace WashGo.Application.Features.MerchantApplication.Services;

/// <summary>Call inside an explicit transactional UoW. Lock operation first, Merchant capacity second.</summary>
public class MerchantWriteGuard : ITransientDependency
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IRepository<MerchantWriteOperation, Guid> _repo;
    private readonly IWashOrderRepository _locks;
    public MerchantWriteGuard(IRepository<MerchantWriteOperation, Guid> repo, IWashOrderRepository locks)
    { _repo = repo; _locks = locks; }

    // Opaque string: .NET ticks/microseconds exceed JavaScript's safe integer range.
    public static string Version(DateTime timestamp) => (timestamp.Ticks / 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string Version(Merchant merchant) => Version(merchant.UpdatedAt);
    public static void Touch(Merchant merchant)
    {
        var micros = Math.Max(DateTime.UtcNow.Ticks / 10, merchant.UpdatedAt.Ticks / 10 + 1);
        merchant.UpdatedAt = new DateTime(micros * 10, DateTimeKind.Utc);
    }
    public static string Hash(object payload) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json)))).ToLowerInvariant();

    public async Task<BaseResponse<T>?> ReplayAsync<T>(Guid? id, Guid actor, Guid merchant, string operation, object payload)
    {
        if (!id.HasValue) return null;
        if (id == Guid.Empty) return Error<T>(HttpStatusCode.BadRequest, "InvalidOperationId", "operationId không hợp lệ.");
        await _locks.AcquireLockAsync($"merchant-write-operation:{id}");
        var previous = await _repo.FindAsync(id.Value);
        if (previous == null) return null;
        if (previous.ActorId != actor || previous.MerchantId != merchant ||
            previous.Operation != operation || previous.PayloadHash != Hash(payload))
            return Error<T>(HttpStatusCode.Conflict, "OperationIdReused", "operationId đã dùng cho yêu cầu khác.");
        return JsonSerializer.Deserialize<BaseResponse<T>>(previous.ResponseJson, Json)
            ?? throw new InvalidOperationException("Invalid Merchant operation response.");
    }

    public Task LockMerchantAsync(Guid id) => _locks.AcquireLockAsync($"merchant-capacity:{id}");
    public async Task RecordAsync<T>(Guid? id, Guid actor, Guid merchant, string operation, object payload, BaseResponse<T> response)
    {
        if (!id.HasValue) return;
        await _repo.InsertAsync(new MerchantWriteOperation(id.Value)
        {
            ActorId = actor, MerchantId = merchant, Operation = operation, PayloadHash = Hash(payload),
            ResponseJson = JsonSerializer.Serialize(response, Json), CreatedAt = DateTime.UtcNow
        }, autoSave: true);
    }
    public Task<MerchantWriteOperation?> FindAsync(Guid id, Guid actor) =>
        _repo.FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actor);

    public static BaseResponse<T> Error<T>(HttpStatusCode status, string code, string message, T? data = default) => new()
    { Succeeded = false, StatusCode = status, Meta = new { code }, Message = message, Data = data };
}
