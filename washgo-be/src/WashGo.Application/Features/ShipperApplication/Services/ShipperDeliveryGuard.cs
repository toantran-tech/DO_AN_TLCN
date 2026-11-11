using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

/// <summary>Called while the order row is locked; shares the existing operation table, not a second ledger.</summary>
public class ShipperDeliveryGuard(IRepository<DeliveryTask, Guid> tasks, IRepository<OrderOperation, Guid> operations,
    IConfiguration configuration) : ITransientDependency
{
    public async Task<BaseResponse<WashOrderDetailDto>?> CheckAsync(WashOrder order, Guid actor, ShipperDeliveryCommand command,
        DeliveryTaskType type, bool requireGuard, bool canAccess)
    {
        if (command.OperationId is { } id)
        {
            var previous = await operations.FindAsync(id);
            if (previous != null)
                return previous.ActorId == actor && previous.OrderId == order.Id && previous.Operation == Operation(type) &&
                    previous.Request == Fingerprint(command) && previous.ResultJson != null
                    ? new() { Succeeded = true, StatusCode = HttpStatusCode.OK, Data = JsonSerializer.Deserialize<WashOrderDetailDto>(previous.ResultJson),
                        Message = "Thao tác đã được thực hiện trước đó.", Meta = new { code = "Replayed", replayed = true, operationId = id } }
                    : Error("OperationIdReused", "operationId đã được sử dụng cho yêu cầu khác.");
        }
        if (!canAccess) return Error("NotFound", "Không tìm thấy đơn hàng.", HttpStatusCode.NotFound);
        if (requireGuard && (command.OperationId is null || command.TaskId is null || command.ExpectedAssignmentVersion is null || command.ExpectedTaskType is null || command.ExpectedRevision is null))
            return Error("DeliveryGuardRequired", "Cần taskId, expectedAssignmentVersion, expectedTaskType, expectedRevision và operationId.", HttpStatusCode.BadRequest);
        if (command.OperationId == Guid.Empty || command.TaskId == Guid.Empty) return Error("InvalidOperationId", "Mã thao tác/nhiệm vụ không hợp lệ.", HttpStatusCode.BadRequest);
        if (command.TaskId.HasValue)
        {
            var task = await tasks.FindAsync(command.TaskId.Value);
            if (task == null || task.OrderId != order.Id || task.ShipperId != order.ShipperId || task.TaskType != type ||
                task.Status != DeliveryTaskStatus.Assigned || command.ExpectedAssignmentVersion != task.Id || command.ExpectedTaskType != type)
                return Error("AssignmentChanged", "Phân công đã thay đổi; vui lòng tải lại nhiệm vụ.");
        }
        if (command.ExpectedRevision.HasValue && command.ExpectedRevision != order.Revision) return Error("RevisionMismatch", "Đơn đã thay đổi; vui lòng tải lại.");
        if (command.ExpectedStatus.HasValue && command.ExpectedStatus != order.Status) return Error("StatusMismatch", "Trạng thái đơn đã thay đổi.");
        var hasPin = !string.IsNullOrWhiteSpace(command.PinCode);
        var hasQr = !string.IsNullOrWhiteSpace(command.QrCode);
        if (hasPin == hasQr || hasPin && (command.PinCode!.Length != 6 || command.PinCode.Any(c => c is < '0' or > '9')) || hasQr && command.QrCode!.Length > 500)
            return Error("InvalidTaskCredential", "Gửi đúng một PIN 6 số hoặc QR tối đa 500 ký tự.", HttpStatusCode.BadRequest);
        return null;
    }

    public async Task RecordAsync(WashOrder order, Guid actor, ShipperDeliveryCommand command, DeliveryTaskType type, WashOrderDetailDto result)
    {
        if (command.OperationId is not { } id) return;
        result = JsonSerializer.Deserialize<WashOrderDetailDto>(JsonSerializer.Serialize(result))!;
        result.RedactForMerchant();
        result.ShipperTaskQrString = null;
        result.CustomerNote = null;
        result.Timeline.Clear();
        await operations.InsertAsync(new OrderOperation(id) { OrderId = order.Id, ActorId = actor, Operation = Operation(type),
            Request = Fingerprint(command), DeliveryTaskId = command.TaskId, ResultStatus = order.Status, ResultRevision = order.Revision,
            ResultJson = JsonSerializer.Serialize(result), CreatedAt = DateTime.UtcNow }, autoSave: true);
    }

    public async Task<OrderOperation?> FindAsync(Guid taskId, Guid operationId, Guid actor) =>
        await operations.FirstOrDefaultAsync(x => x.Id == operationId && x.DeliveryTaskId == taskId && x.ActorId == actor && x.ResultJson != null);

    private string Fingerprint(ShipperDeliveryCommand command)
    {
        var key = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT key required for operation HMAC.");
        var payload = JsonSerializer.Serialize(new { command.TaskId, command.ExpectedAssignmentVersion, command.ExpectedTaskType,
            command.ExpectedRevision, command.ExpectedStatus, command.PinCode, command.QrCode });
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(payload)));
    }

    private static string Operation(DeliveryTaskType type) => type == DeliveryTaskType.Collect ? "ShipperCollect" : "ShipperReturn";

    public static BaseResponse<WashOrderDetailDto> Error(string code, string message, HttpStatusCode status = HttpStatusCode.Conflict) =>
        new() { Succeeded = false, StatusCode = status, Message = message, Meta = new { code, serverNow = DateTime.UtcNow } };

    public static BaseResponse<WashOrderDetailDto> CredentialError(WashOrder order, bool locked) => new()
    {
        Succeeded = false, StatusCode = HttpStatusCode.BadRequest,
        Message = locked ? "Mã nhiệm vụ tạm khóa; vui lòng chờ hết thời gian khóa." : "Mã nhiệm vụ không chính xác.",
        Meta = new { code = locked ? "TaskLocked" : "InvalidTaskCredential", remainingAttempts = Math.Max(0, 5 - order.TaskPinAttemptCount),
            lockoutUntil = order.TaskPinLockoutUntil, serverNow = DateTime.UtcNow }
    };
}
