using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Results
{
    public class ServiceAreaResultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal RadiusKm { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Số Merchant đang được gán phục vụ vùng.</summary>
        public int MerchantCount { get; set; }
        public long CreatedOn { get; set; }
    }

    public class AreaMerchantDto
    {
        public Guid MerchantId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public bool Assigned { get; set; }
        public int? Priority { get; set; }
    }

    public class AreaMerchantsDto
    {
        public ServiceAreaResultDto Area { get; set; } = new();
        public List<AreaMerchantDto> Merchants { get; set; } = [];
    }
}
