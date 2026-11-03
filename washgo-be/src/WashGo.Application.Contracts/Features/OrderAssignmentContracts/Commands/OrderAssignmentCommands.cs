using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.OrderAssignmentContracts.Commands
{
    /// <summary>UC 3.3.2: Admin chọn Merchant cho đơn.</summary>
    public class AssignOrderCommand
    {
        public Guid MerchantId { get; set; }
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
    }

    public class AutoAssignItem
    {
        public Guid OrderId { get; set; }
        public Guid MerchantId { get; set; }
    }
}
