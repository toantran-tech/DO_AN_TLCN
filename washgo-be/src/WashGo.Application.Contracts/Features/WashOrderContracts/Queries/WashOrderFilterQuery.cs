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
        /// <summary>
        /// Lọc đơn đang quá hạn theo giờ server: DepositExpired (chờ gửi, quá hạn gửi đồ), PaymentOverdue (chờ thanh toán,
        /// quá hạn thanh toán), PickupOverdue (đồ ở tủ trả, quá hạn lấy). Áp dụng cho cả danh sách và summary;
        /// fromDate/toDate vẫn là ngày tạo đơn.
        /// </summary>
        public string? OverdueType { get; set; }
    }
}
