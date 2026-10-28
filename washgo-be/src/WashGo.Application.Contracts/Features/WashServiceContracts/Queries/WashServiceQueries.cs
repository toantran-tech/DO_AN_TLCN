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
        public WashServiceType? ServiceType { get; set; }
        public bool? IsActive { get; set; }
    }
}

