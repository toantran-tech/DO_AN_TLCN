using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Queries
{
    public class WashServiceFilterQuery : BaseFilterQuery
    {
        public Guid[]? CategoryIds { get; set; }
        /// <summary>Lọc legacy: tương đương lọc theo danh mục gốc của loại này; nên dùng categoryIds.</summary>
        public WashServiceType? ServiceType { get; set; }
        public bool? IsActive { get; set; }
        public Guid? MerchantId { get; set; }
        public ServiceApprovalStatus? ApprovalStatus { get; set; }
    }
}

