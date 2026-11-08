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
using WashGo.Domain.Services.Identity;

namespace WashGo.Application.Features.MerchantApplication.Services;

/// <summary>Call inside an explicit transactional UoW. Lock operation first, Merchant capacity second.</summary>
public class MerchantWriteGuard : ITransientDependency
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IRepository<MerchantWriteOperation, Guid> _repo;
    private readonly IWashOrderRepository _locks;
    private readonly IIdentityConflictClassifier _conflicts;
    public MerchantWriteGuard(IRepository<MerchantWriteOperation, Guid> repo, IWashOrderRepository locks, IIdentityConflictClassifier conflicts)
    { _repo = repo; _locks = locks; _conflicts = conflicts; }

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

    public async Task<BaseResponse<T>?> ReplayAsync<T>(Guid? id, Guid actor, Guid? merchant, string operation, object payload)
    {
        if (!id.HasValue) return null;
        if (id == Guid.Empty) return Error<T>(HttpStatusCode.BadRequest, "InvalidOperationId", "operationId không hợp lệ.");
        await _locks.AcquireLockAsync($"merchant-write-operation:{id}");
        var previous = await _repo.FindAsync(id.Value);
        if (previous == null) return null;
        if (previous.ActorId != actor || (merchant.HasValue && previous.MerchantId != merchant) ||
            previous.Operation != operation || previous.PayloadHash != Hash(payload))
            return Error<T>(HttpStatusCode.Conflict, "OperationIdReused", "operationId đã dùng cho yêu cầu khác.");
        return JsonSerializer.Deserialize<BaseResponse<T>>(previous.ResponseJson, Json)
            ?? throw new InvalidOperationException("Invalid Merchant operation response.");
    }

    public Task LockMerchantAsync(Guid id) => _locks.AcquireLockAsync($"merchant-capacity:{id}");
    public async Task LockIdentityAsync(string email, string? taxCode)
    {
        // Email then tax is the global ordering for identity creation. Locks contain hashes, not raw PII.
        await _locks.AcquireLockAsync($"merchant-email:{Hash(email)}");
        if (!string.IsNullOrWhiteSpace(taxCode))
            await _locks.AcquireLockAsync($"merchant-tax:{Hash(taxCode)}");
    }

    public async Task<BaseResponse<T>> IdentitySafeAsync<T>(Func<Task<BaseResponse<T>>> action)
    {
        try { return await action(); }
        catch (Exception error) when (_conflicts.Classify(error) != null)
        {
            var code = _conflicts.Classify(error)!;
            return Error<T>(HttpStatusCode.Conflict, code,
                code == "EmailAlreadyUsed" ? "Email này đã được sử dụng." : "Mã số thuế này đã được sử dụng.");
        }
    }
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
