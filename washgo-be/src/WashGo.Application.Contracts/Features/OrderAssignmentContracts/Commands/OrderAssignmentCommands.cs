using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.OrderAssignmentContracts.Commands
{
    /// <summary>UC 3.3.2: Admin chọn Merchant cho đơn.</summary>
    public class AssignOrderCommand
    {
        public Guid MerchantId { get; set; }
        /// <summary>`revision` của đơn lúc Admin xem; khác bản hiện tại → 409 RevisionMismatch.</summary>
        public long? ExpectedRevision { get; set; }
        /// <summary>Tỷ lệ hoa hồng của Merchant Admin đã xem ở danh sách gợi ý; khác hiện tại → 409 CommissionChanged.</summary>
        public decimal? ExpectedCommissionRate { get; set; }
        /// <summary>Mã thao tác (hoặc header Idempotency-Key): gửi lại trả kết quả cũ, cùng mã khác nội dung → 409.</summary>
        public Guid? OperationId { get; set; }
    }

    /// <summary>UC 3.3.3: các đơn cần hệ thống gợi ý Merchant.</summary>
    public class AutoSuggestCommand
    {
        public List<Guid> OrderIds { get; set; } = [];
    }

    /// <summary>UC 3.3.3: Admin xác nhận các cặp đơn – Merchant đã được gợi ý.</summary>
    public class AutoAssignCommand
    {
        public List<AutoAssignItem> Assignments { get; set; } = [];
        /// <summary>Mã thao tác cho cả lô (hoặc header Idempotency-Key).</summary>
        public Guid? OperationId { get; set; }
    }

    public class AutoAssignItem
    {
        public Guid OrderId { get; set; }
        public Guid MerchantId { get; set; }
        public long? ExpectedRevision { get; set; }
        public decimal? ExpectedCommissionRate { get; set; }
    }
}
