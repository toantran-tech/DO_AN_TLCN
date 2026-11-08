using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Điều kiện ghi cho thao tác Merchant/Admin trên đơn. Gọi sau khi đã khóa dòng đơn (FOR UPDATE):
    /// 1. operationId đã có → trả lại kết quả cũ (Replay) hoặc 409 khi key bị dùng cho yêu cầu khác;
    /// 2. expectedRevision/expectedStatus lệch dữ liệu hiện tại → 409.
    /// Thao tác thành công được ghi cùng transaction với thay đổi của đơn.
    /// </summary>
    public class OrderWriteGuard : ITransientDependency
    {
        public enum Outcome { Proceed, Replay, Conflict, Invalid }

        public sealed record Decision(Outcome Outcome, string? Code = null, string? Message = null, OrderOperation? Previous = null);

        private readonly IRepository<OrderOperation, Guid> _operations;

        public OrderWriteGuard(IRepository<OrderOperation, Guid> operations) => _operations = operations;

        public async Task<Decision> CheckAsync(WashOrder order, Guid actorId, GuardedOrderCommand command, string operation, string request)
        {
            if (command.OperationId is { } operationId)
            {
                if (operationId == Guid.Empty)
                    return new(Outcome.Invalid, "InvalidOperationId", "operationId không hợp lệ.");
                var previous = await _operations.FindAsync(operationId);
                if (previous != null)
                    return previous.OrderId == order.Id && previous.ActorId == actorId &&
                           previous.Operation == operation && previous.Request == request
                        ? new(Outcome.Replay, "Replayed", "Thao tác này đã được xử lý trước đó; không thực hiện lại.", previous)
                        : new(Outcome.Conflict, "OperationIdReused", "operationId này đã được dùng cho một thao tác khác.");
            }
            if (command.ExpectedRevision is { } revision && revision != order.Revision)
                return new(Outcome.Conflict, "RevisionMismatch", "Đơn hàng đã thay đổi kể từ lúc bạn xem; vui lòng tải lại trước khi thao tác.");
            if (command.ExpectedStatus is { } status && status != order.Status)
                return new(Outcome.Conflict, "StatusMismatch", $"Đơn hàng đã chuyển sang {order.Status}; vui lòng tải lại trước khi thao tác.");
            return new(Outcome.Proceed);
        }

        public async Task RecordAsync(WashOrder order, Guid actorId, GuardedOrderCommand command, string operation, string request)
        {
            if (command.OperationId is not { } operationId)
                return;
            await _operations.InsertAsync(new OrderOperation(operationId)
            {
                OrderId = order.Id,
                ActorId = actorId,
                Operation = operation,
                Request = request,
                ResultStatus = order.Status,
                ResultRevision = order.Revision,
                CreatedAt = DateTime.UtcNow
            }, autoSave: true);
        }

        public Task<OrderOperation?> FindAsync(Guid operationId) => _operations.FindAsync(operationId);

        /// <summary>
        /// Request fingerprint: readable key fields plus a hash of the free-text part (note/reason), so a replay with the
        /// same key but a different note is a different request (409) and the value fits the 200-char column.
        /// </summary>
        public static string Fingerprint(string fields, string? text) => text == null
            ? fields
            : $"{fields};text={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant()}";
    }
}
