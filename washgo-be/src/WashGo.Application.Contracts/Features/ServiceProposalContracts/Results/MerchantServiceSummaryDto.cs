using System;

namespace WashGo.Application.Contracts.Features.ServiceProposalContracts.Results
{
    /// <summary>Thống kê danh mục dịch vụ chung và đề xuất của Merchant hiện tại, một lần đếm, không theo bộ lọc bảng.</summary>
    public class MerchantServiceSummaryDto
    {
        /// <summary>Dịch vụ chung đã duyệt, chưa xóa.</summary>
        public int TotalServices { get; set; }
        public int ActiveServices { get; set; }
        public int InactiveServices { get; set; }
        /// <summary>Đề xuất của chính Merchant theo trạng thái.</summary>
        public int PendingProposals { get; set; }
        public int ApprovedProposals { get; set; }
        public int RejectedProposals { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
