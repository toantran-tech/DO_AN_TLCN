using System;
namespace WashGo.Application.Contracts.Features.FinanceContracts.Queries
{
    /// <summary>Đơn cần đối chiếu tỷ lệ hoa hồng: thiếu tỷ lệ mà đã có thu tiền giặt, hoặc 0% cũ chưa xác minh. page từ 1, take ≤ 100.</summary>
    public class CommissionReviewQuery
    {
        public int? Page { get; set; }
        public int? Take { get; set; }
        public Guid? MerchantId { get; set; }
    }
}
