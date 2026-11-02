using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Queries
{
    public class ShipperFilterQuery : BaseFilterQuery
    {
        public ShipperStatus? Status { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>Chỉ Admin dùng; Merchant luôn bị giới hạn trong tiệm của mình.</summary>
        public Guid? MerchantId { get; set; }
    }
}
