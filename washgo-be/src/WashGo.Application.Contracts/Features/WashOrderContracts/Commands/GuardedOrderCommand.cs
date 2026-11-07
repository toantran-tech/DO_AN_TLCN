using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Commands
{
    /// <summary>
    /// Điều kiện ghi dùng chung cho status / actual-weight / assign-shipper (tất cả đều tùy chọn).
    /// Lệch phiên bản hoặc trạng thái trả 409 kèm dữ liệu đơn hiện tại.
    /// </summary>
    public abstract class GuardedOrderCommand
    {
        /// <summary>`revision` của đơn lúc người dùng xem (hoặc header If-Match).</summary>
        public long? ExpectedRevision { get; set; }
        /// <summary>Trạng thái đơn lúc người dùng xem.</summary>
        public WashOrderStatus? ExpectedStatus { get; set; }
        /// <summary>Mã thao tác FE sinh một lần cho mỗi ý định (hoặc header Idempotency-Key); gửi lại không thực hiện lại.</summary>
        public Guid? OperationId { get; set; }
    }
}
