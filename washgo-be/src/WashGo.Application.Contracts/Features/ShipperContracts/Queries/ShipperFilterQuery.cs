using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries
{
    /// <summary>
    /// Lọc Shipper. sortBy hỗ trợ field "Rating" (isDescending true/false), thứ tự phụ FullName rồi Id;
    /// Shipper chưa có đánh giá (Rating = 0) luôn đứng cuối. Không truyền sortBy: đang làm việc trước, rồi theo tên.
    /// </summary>
    public class ShipperFilterQuery : BaseFilterQuery
    {
        public ShipperStatus? Status { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>Chỉ Admin dùng; Merchant luôn bị giới hạn trong tiệm của mình.</summary>
        public Guid? MerchantId { get; set; }
    }
}
