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
        Func<AdministrativeBatchItem, Guid, Task<AdministrativeBatchItemResultDto>> apply)
    {
        if (!user.IsInRole("Admin") || !user.Id.HasValue) return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.Forbidden, "AdminOnly", "Chỉ Admin được thao tác hàng loạt.");
        if (command.OperationId == Guid.Empty || command.Items == null || command.Items.Count < 1 || command.Items.Count > maximum ||
            command.Items.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.ExpectedVersion)) || command.Items.Select(x => x.Id).Distinct().Count() != command.Items.Count ||
            command.Action is not ("Delete" or "Status"))
            return AdministrativeWriteGuard.Error<AdministrativeBatchResultDto>(HttpStatusCode.BadRequest, "InvalidBatch", $"Cần operationId, 1–{maximum} ID khác nhau kèm expectedVersion và action=Delete|Status.");
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
        var response = new BaseResponse<AdministrativeBatchResultDto> { Succeeded = true, StatusCode = HttpStatusCode.OK, Data = result, Message = "Đã xử lý; xem kết quả từng ID." };
        operation.ResponseJson = JsonSerializer.Serialize(response, Json); operation.CompletedAt = DateTime.UtcNow;
        await operations.UpdateAsync(operation, autoSave: true); await finish.CompleteAsync(); return response;
    }
    public static AdministrativeBatchItemResultDto Item<T>(Guid id, BaseResponse<T> response) => new() { Id = id, Succeeded = response.Succeeded, StatusCode = (int)response.StatusCode, Message = response.Message };
}
