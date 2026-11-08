using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    /// <summary>Kết quả dự kiến khi Admin hủy đơn quá hạn gửi đồ (chỉ đọc, không giải phóng ô hay ghi phí).</summary>
    public class AdminCancellationQuoteDto
    {
        public Guid OrderId { get; set; }
        public WashOrderStatus Status { get; set; }
        public long Revision { get; set; }
        public bool CanCancel { get; set; }
        /// <summary>RoleNotAllowed, NotPending, NotExpired, RefundInProgress, NoVerifiedPayment.</summary>
        public string? ReasonCode { get; set; }
        public string? Reason { get; set; }
        public DateTime DepositDeadline { get; set; }
        public DateTime ServerNow { get; set; }
        public decimal BasisAmount { get; set; }
        public int FeePercent { get; set; }
        /// <summary>Phí hủy theo chính sách hủy hiện hành (đơn chờ gửi quá hạn).</summary>
        public decimal FeeAmount { get; set; }
        /// <summary>Tiền đã thu được xác minh qua giao dịch.</summary>
        public decimal PaidAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal FeeOutstanding { get; set; }
    }
}
