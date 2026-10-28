using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Results
{
    public class WashServiceResultDto
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public WashServiceType ServiceType { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int EstimatedDurationHours { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; }
        public bool IsActive { get; set; }
        public long CreatedOn { get; set; }
    }
}

