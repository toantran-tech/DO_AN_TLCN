using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands
{
    public class CreateServiceAreaCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal RadiusKm { get; set; } = 2;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateServiceAreaCommand : CreateServiceAreaCommand
    {
        public Guid Id { get; set; }
    }

    /// <summary>UC 3.2.5: danh sách Merchant phục vụ vùng kèm độ ưu tiên (1 = cao nhất).</summary>
    public class SetAreaMerchantsCommand
    {
        public List<AreaMerchantItem> Merchants { get; set; } = [];
        /// <summary>Xác nhận lưu khi bỏ chọn tất cả Merchant (UC 3.2.5 5a).</summary>
        public bool ConfirmEmpty { get; set; }
    }

    public class AreaMerchantItem
    {
        public Guid MerchantId { get; set; }
        public int Priority { get; set; } = 1;
    }
}
