using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Queries
{
    public class MerchantFilterQuery : BaseFilterQuery
    {
        public MerchantStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

