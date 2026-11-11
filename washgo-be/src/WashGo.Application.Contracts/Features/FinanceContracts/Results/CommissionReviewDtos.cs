using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Results
{
    public class CommissionReviewItemDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        /// <summary>Tỷ lệ đang áp dụng trên đơn (null = thiếu).</summary>
        public decimal? AppliedRate { get; set; }
        /// <summary>Tỷ lệ hiện tại của Merchant — chỉ để tham khảo, không phải bằng chứng.</summary>
        public decimal? MerchantCurrentRate { get; set; }
        public string? Source { get; set; }
        /// <summary>Missing hoặc LegacyUnverified.</summary>
        public string Reason { get; set; } = string.Empty;
        public DateTime? LockedAt { get; set; }
        public decimal CollectedLaundryAmount { get; set; }
        /// <summary>true khi đã có khoản thu/hoàn của đơn nằm trong kỳ đối soát: không thể sửa tỷ lệ tại đây.</summary>
        public bool InSettlement { get; set; }
        /// <summary>Có lịch sử "Admin phân công" cho đơn (căn cứ tham khảo).</summary>
        public bool HasAssignmentHistory { get; set; }
    }

    public class CommissionReviewPageDto
    {
        public int Page { get; set; }
        public int Take { get; set; }
        public int Total { get; set; }
        public List<CommissionReviewItemDto> Items { get; set; } = [];
        public DateTime GeneratedAt { get; set; }
    }
}
