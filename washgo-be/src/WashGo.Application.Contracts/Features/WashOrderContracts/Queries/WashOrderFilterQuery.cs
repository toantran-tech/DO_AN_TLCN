using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Queries
{
    public class WashOrderFilterQuery : BaseFilterQuery
    {
        public WashOrderStatus? Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public Guid? LockerId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ShipperId { get; set; }
        public Guid? MerchantId { get; set; }
        /// <summary>Lọc theo loại nhiệm vụ giao nhận (UC 4.3.1).</summary>
        public DeliveryTaskType? TaskType { get; set; }
        /// <summary>Chỉ đơn có nhiệm vụ lấy/trả chưa hoàn tất.</summary>
        public bool? PendingOnly { get; set; }
    }
}
