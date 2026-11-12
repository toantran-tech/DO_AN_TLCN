using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Audit;

namespace WashGo.Application.Features.AuditApplication.Services;

public class AdministrativeBatchExecutor(IUnitOfWorkManager units, AdministrativeWriteGuard guard,
    IRepository<AdministrativeOperation, Guid> operations, ICurrentUser user) : ITransientDependency
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<BaseResponse<AdministrativeBatchResultDto>> RunAsync(string module, AdministrativeBatchCommand command, int maximum,
        Func<AdministrativeBatchItem, Guid, Task<AdministrativeBatchItemResultDto>> apply, string[]? actions = null)
    {
        actions ??= ["Delete", "Status"];
        if (!user.IsInRole("Admin") || !user.Id.HasValue) return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được thao tác hàng loạt.");
        if (command.OperationId == Guid.Empty || command.Items == null || command.Items.Count < 1 || command.Items.Count > maximum ||
            command.Items.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.ExpectedVersion)) || command.Items.Select(x => x.Id).Distinct().Count() != command.Items.Count ||
            !actions.Contains(command.Action))
            return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.BadRequest, "InvalidBatch", $"Cần operationId, 1–{maximum} ID khác nhau kèm expectedVersion và action={string.Join('|', actions)}.");
        var hash = AdministrativeWriteGuard.Version(command);
        // Persist the intent first: crash recovery cannot reuse a key for a different batch.
        using (var reserve = units.Begin(requiresNew: true, isTransactional: true))
        {
            await guard.LockAsync($"admin-operation:{command.OperationId}");
            var existing = await operations.FindAsync(command.OperationId);
            if (existing != null && (existing.ActorId != user.Id || existing.Module != module || existing.Action != "Batch" || existing.PayloadHash != hash))
                return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.Conflict, "OperationIdReused", "operationId đã dùng cho yêu cầu khác.");
            if (existing == null)
                await operations.InsertAsync(new AdministrativeOperation(command.OperationId) { ActorId = user.Id.Value, Module = module, Action = "Batch", ResourceId = "batch", PayloadHash = hash,
                    ResponseJson = JsonSerializer.Serialize(new BaseResponse<AdministrativeBatchResultDto> { Succeeded = true, StatusCode = HttpStatusCode.Accepted, Data = new() { OperationId = command.OperationId, State = "Processing" } }, Json), CompletedAt = DateTime.UtcNow }, autoSave: true);
            await reserve.CompleteAsync();
        }
        using var finish = units.Begin(requiresNew: true, isTransactional: true);
        await guard.LockAsync($"admin-operation:{command.OperationId}");
        var operation = await operations.GetAsync(command.OperationId);
        var previous = JsonSerializer.Deserialize<BaseResponse<AdministrativeBatchResultDto>>(operation.ResponseJson, Json)!;
        if (previous.Data?.State == "Completed") return previous;
        var result = new AdministrativeBatchResultDto { OperationId = command.OperationId };
        foreach (var item in command.Items)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{command.OperationId:N}:{item.Id:N}"));
            // Each child commits separately and has a deterministic operation key, so retry after a crash resumes safely.
            result.Items.Add(await apply(item, new Guid(bytes.AsSpan(0, 16))));
        }
        result.Totals = new()
        {
            Changed = result.Items.Count(x => x.Outcome == "Changed"), Unchanged = result.Items.Count(x => x.Outcome == "Unchanged"),
            Skipped = result.Items.Count(x => x.Outcome == "Skipped"), Failed = result.Items.Count(x => x.Outcome == "Failed")
        };
        result.Totals.Succeeded = result.Totals.Changed + result.Totals.Unchanged;
        var response = new BaseResponse<AdministrativeBatchResultDto> { Succeeded = true, StatusCode = HttpStatusCode.OK, Data = result, Message = "Đã xử lý; xem kết quả từng ID." };
        operation.ResponseJson = JsonSerializer.Serialize(response, Json); operation.CompletedAt = DateTime.UtcNow;
        await operations.UpdateAsync(operation, autoSave: true); await finish.CompleteAsync(); return response;
    }
    public static AdministrativeBatchItemResultDto Item<T>(Guid id, BaseResponse<T> response)
    {
        var code = MetaValue(response.Meta, "code");
        var outcome = response.Succeeded ? (MetaValue(response.Meta, "outcome") == "Unchanged" ? "Unchanged" : "Changed")
            : response.StatusCode == HttpStatusCode.NotFound ? "Skipped" : "Failed";
        object? data = response.Succeeded ? response.Data : null;
        if (data is JsonElement { ValueKind: JsonValueKind.Object } json)
            data = new JsonDataView(json);
        string? Prop(string name) => data is JsonDataView v ? v.Get(name) : data?.GetType().GetProperty(name)?.GetValue(data)?.ToString();
        bool? Flag(string name) => bool.TryParse(Prop(name), out var b) ? b : null;
        return new()
        {
            Version = Prop("Version"), IsHidden = Flag("IsHidden"), IsActive = Flag("IsActive"),
            Id = id, Succeeded = response.Succeeded, StatusCode = (int)response.StatusCode, Message = response.Message, Outcome = outcome,
            ReasonCode = outcome switch
            {
                "Changed" => null,
                "Unchanged" => "AlreadyInTargetState",
                "Skipped" => code ?? "NotFound",
                _ => code ?? (response.StatusCode == HttpStatusCode.BadRequest ? "Rejected" : response.StatusCode.ToString())
            }
        };
    }

    private sealed class JsonDataView(JsonElement json)
    {
        public string? Get(string name)
        {
            var p = json.EnumerateObject().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return p.Value.ValueKind switch { JsonValueKind.String => p.Value.GetString(), JsonValueKind.True => "True", JsonValueKind.False => "False", _ => null };
        }
    }

    /// <summary>Reads Meta.{name} whether Meta is an in-process anonymous object or a replayed JSON element.</summary>
    public static string? MetaValue(object? meta, string name) => meta switch
    {
        null => null,
        JsonElement { ValueKind: JsonValueKind.Object } json => json.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null,
        _ => meta.GetType().GetProperty(name)?.GetValue(meta)?.ToString()
    };

    /// <summary>Marks a successful response whose resource already had the requested state.</summary>
    public static BaseResponse<T> Unchanged<T>(BaseResponse<T> response) { response.Meta = new { outcome = "Unchanged" }; return response; }

    /// <summary>Item result for a validation that failed before the single-item command ran.</summary>
    public static AdministrativeBatchItemResultDto Invalid(Guid id, string code, string message) =>
        new() { Id = id, StatusCode = 400, Message = message, Outcome = "Failed", ReasonCode = code };
}
