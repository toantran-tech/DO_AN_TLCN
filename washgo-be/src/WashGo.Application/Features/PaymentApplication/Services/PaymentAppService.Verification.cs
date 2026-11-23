using System.Net;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService : IPaymentVerificationRunner
{
    private IRepository<PaymentVerification, Guid> Verifications => LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentVerification, Guid>>();

    [UnitOfWork(IsDisabled = true)]
    public async Task<BaseResponse<PaymentVerificationDto>> VerifySessionAsync(Guid orderId, VerifyPaymentSessionCommand command)
    {
        if (!CustomerSignedIn) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được kiểm tra thanh toán của mình." };
        if (!ValidPurpose(command.Purpose) || command.OperationId == Guid.Empty)
            return BadRequest<PaymentVerificationDto>("Cần operationId hợp lệ và purpose 1, 2 hoặc 3.");
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var order = await _orders.FindAsync(orderId);
        if (order == null || order.CustomerId != CurrentUser.Id) return NotFound<PaymentVerificationDto>("Không tìm thấy đơn hàng của bạn.");
        await VerificationLockAsync(command.OperationId);
        var old = await Verifications.FindAsync(command.OperationId);
        if (old != null)
        {
            if (old.CustomerId != CurrentUser.Id || old.OrderId != orderId || old.Purpose != command.Purpose)
                return new() { StatusCode = HttpStatusCode.Conflict, Message = "operationId đã được dùng cho yêu cầu khác.", Meta = new { code = "OperationIdReused" } };
            return Success(await VerificationViewAsync(old));
        }
        await _orders.AcquireLockAsync($"payment-verify-scope:{orderId}:{(int)command.Purpose}");
        var blockers = await BlockersAsync(orderId, command.Purpose);
        var operation = new PaymentVerification(command.OperationId)
        { CustomerId = CurrentUser.Id!.Value, OrderId = orderId, Purpose = command.Purpose };
        if (blockers.Count != 1 || blockers[0].RequiresReconciliation)
        {
            operation.IsActive = false;
            operation.State = blockers.Count == 0 ? "Completed" : "NeedsReview";
            operation.ReasonCode = blockers.Count == 0 ? "NoActiveSession" : "LegacyOrReceiptRequiresReview";
        }
        else
        {
            var session = PaymentSessionRules.View(orderId, command.Purpose, blockers, DateTime.UtcNow, UseLiveGateway);
            if (!session.CanVerify)
                return new() { StatusCode = HttpStatusCode.Conflict, Message = "Phiên đang khởi tạo; vui lòng kiểm tra lại sau.", Meta = new { code = "PaymentInitializing", retryAfterSeconds = 2 } };
            operation.TransactionId = blockers[0].Id;
            var prior = await AsyncExecuter.FirstOrDefaultAsync((await Verifications.GetQueryableAsync())
                .Where(x => x.TransactionId == operation.TransactionId && x.ParentOperationId == null)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));
            if (prior != null && (prior.IsActive || prior.UpdatedAt.AddSeconds(60) > DateTime.UtcNow))
            {
                operation.ParentOperationId = prior.Id;
                operation.IsActive = false;
                operation.State = "Following";
            }
        }
        await Verifications.InsertAsync(operation, autoSave: true);
        var dto = await VerificationViewAsync(operation);
        await uow.CompleteAsync();
        return new() { Succeeded = true, StatusCode = dto.State is "Queued" or "Verifying" ? HttpStatusCode.Accepted : HttpStatusCode.OK, Data = dto,
            Message = "Đã ghi nhận yêu cầu. Theo dõi operationId; không tự tạo phiên thay thế khi đang xác minh." };
    }

    public async Task<BaseResponse<PaymentVerificationDto>> GetVerificationAsync(Guid operationId)
    {
        if (!CustomerSignedIn) return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được xem kết quả của mình." };
        var operation = await Verifications.FindAsync(operationId);
        if (operation == null || operation.CustomerId != CurrentUser.Id) return NotFound<PaymentVerificationDto>("Không tìm thấy yêu cầu xác minh.");
        return Success(await VerificationViewAsync(operation));
    }

    private Task VerificationLockAsync(Guid id) => _orders.AcquireLockAsync($"payment-verification:{id}");

    private async Task<PaymentVerificationDto> VerificationViewAsync(PaymentVerification operation)
    {
        var current = operation.ParentOperationId.HasValue ? await Verifications.GetAsync(operation.ParentOperationId.Value) : operation;
        var tx = current.TransactionId.HasValue ? await _transactionRepo.FindAsync(current.TransactionId.Value) : null;
        var resolved = tx != null && (tx.GatewayVerifiedAt.HasValue || tx.VerifiedClosedAt.HasValue);
        return new PaymentVerificationDto
        {
            OperationId = operation.Id, OrderId = operation.OrderId, Purpose = operation.Purpose,
            State = tx?.RequiresReconciliation == true ? "NeedsReview" : resolved ? "Completed" : current.State,
            ReasonCode = tx?.RequiresReconciliation == true ? "PaymentRequiresReview" : resolved ?
                tx!.GatewayVerifiedAt.HasValue ? "GatewayVerifiedSuccess" : "GatewayVerifiedFailure" : current.ReasonCode,
            CreatedAt = operation.CreatedAt, UpdatedAt = current.UpdatedAt,
            RetryAfterSeconds = current.IsActive ? (int)Math.Clamp(Math.Ceiling(((current.LeaseUntil ?? current.NextAttemptAt) - DateTime.UtcNow).TotalSeconds), 2, 120) : null,
            RequiresReconciliation = tx?.RequiresReconciliation == true || current.State == "NeedsReview"
        };
    }

    [RemoteService(false)]
    [UnitOfWork(IsDisabled = true)]
    public async Task DrainVerificationAsync()
    {
        if (!_configuration.GetValue("Payment:Verification:Enabled", true)) return;
        List<Guid> ids;
        using (var scan = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            var now = DateTime.UtcNow;
            ids = await AsyncExecuter.ToListAsync((await Verifications.GetQueryableAsync())
                .Where(x => x.IsActive && (x.LeaseUntil == null && x.NextAttemptAt <= now || x.LeaseUntil <= now))
                .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(5).Select(x => x.Id));
            await scan.CompleteAsync();
        }
        foreach (var id in ids) await ProcessVerificationAsync(id);
    }

    private async Task ProcessVerificationAsync(Guid id)
    {
        Guid lease;
        GatewayQueryRequest? query = null;
        using (var claim = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            await VerificationLockAsync(id);
            var operation = await Verifications.FindAsync(id);
            var now = DateTime.UtcNow;
            if (operation == null || !operation.IsActive || operation.LeaseUntil > now || operation.LeaseUntil == null && operation.NextAttemptAt > now) return;
            var tx = operation.TransactionId.HasValue ? await _transactionRepo.FindAsync(operation.TransactionId.Value) : null;
            if (tx == null) { operation.State = "NeedsReview"; operation.IsActive = false; operation.ReasonCode = "TransactionUnavailable"; await Verifications.UpdateAsync(operation, autoSave: true); await claim.CompleteAsync(); return; }
            if (tx.GatewayVerifiedAt.HasValue || tx.VerifiedClosedAt.HasValue)
            {
                operation.IsActive = false; operation.UpdatedAt = now;
                operation.State = tx.RequiresReconciliation ? "NeedsReview" : "Completed";
                operation.ReasonCode = tx.RequiresReconciliation ? "PaymentRequiresReview" :
                    tx.GatewayVerifiedAt.HasValue ? "GatewayVerifiedSuccess" : "GatewayVerifiedFailure";
                operation.LeaseUntil = null; operation.LeaseToken = null;
                await Verifications.UpdateAsync(operation, autoSave: true);
                await claim.CompleteAsync();
                return;
            }
            lease = Guid.NewGuid(); operation.LeaseToken = lease; operation.LeaseUntil = now.AddMinutes(2);
            operation.Attempts++; operation.State = "Verifying"; operation.UpdatedAt = now;
            await Verifications.UpdateAsync(operation, autoSave: true);
            query = new GatewayQueryRequest(tx.PaymentMethod, tx.TransactionReference, tx.Amount,
                tx.GatewayCreatedAt ?? PaymentSessionRules.GatewayDate(tx, "vnp_CreateDate"), lease.ToString("N"));
            await claim.CompleteAsync();
        }
        // No SQL transaction, row or advisory lock survives the external HTTP call.
        var result = UseLiveGateway ? await LazyServiceProvider.LazyGetRequiredService<PaymentQueryGateway>().QueryAsync(query) :
            new GatewayQueryResult(GatewayQueryOutcome.Unknown, "MockQueryDoesNotSimulateMoney");
        using var finish = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await VerificationLockAsync(id);
        var op = await Verifications.FindAsync(id);
        if (op == null || !op.IsActive || op.LeaseToken != lease) return;
        var transaction = await _transactionRepo.GetForUpdateByReferenceAsync(query.Reference);
        if (transaction == null) result = new(GatewayQueryOutcome.Unknown, "TransactionUnavailable");
        else if (result.Outcome is GatewayQueryOutcome.Succeeded or GatewayQueryOutcome.Failed)
        {
            await ApplyGatewayResultAsync(transaction, result.Outcome == GatewayQueryOutcome.Succeeded,
                result.ProviderTransactionId, result.GatewayCode is { } code ? $"{code}: verified by gateway query" : result.ReasonCode, "Gateway query");
        }
        if (transaction != null && (transaction.GatewayVerifiedAt.HasValue || transaction.VerifiedClosedAt.HasValue))
        {
            op.State = transaction.RequiresReconciliation ? "NeedsReview" : "Completed";
            op.IsActive = false;
            op.ReasonCode = transaction.RequiresReconciliation ? "PaymentRequiresReview" :
                transaction.GatewayVerifiedAt.HasValue ? "GatewayVerifiedSuccess" : "GatewayVerifiedFailure";
        }
        else
        {
            op.ReasonCode = result.ReasonCode;
            var max = Math.Clamp(_configuration.GetValue("Payment:Verification:MaxAttempts", 5), 1, 10);
            if (op.Attempts >= max)
            {
                op.State = "NeedsReview"; op.IsActive = false;
                if (transaction != null)
                    await MarkPendingVerificationAsync(transaction, "GatewayQueryAttemptsExhausted", transaction.ProviderTransactionId);
                await _notifications.SendToAdminsAsync(NotificationType.SlaWarning, "Thanh toán cần xác minh",
                    $"Đơn {op.OrderId}: chưa xác định kết quả thanh toán sau {op.Attempts} lần kiểm tra. Giữ nguyên phiên và điểm; cần đối soát.", "order", op.OrderId);
            }
            else
            {
                op.State = "Queued";
                op.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(900, 30 * Math.Pow(2, op.Attempts)) + Random.Shared.Next(0, 10));
            }
        }
        op.LeaseUntil = null; op.LeaseToken = null; op.UpdatedAt = DateTime.UtcNow;
        await Verifications.UpdateAsync(op, autoSave: true);
        await finish.CompleteAsync();
    }
}
