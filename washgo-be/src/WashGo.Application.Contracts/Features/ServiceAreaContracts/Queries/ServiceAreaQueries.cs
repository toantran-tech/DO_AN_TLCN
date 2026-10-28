using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries
{
    public class ServiceAreaFilterQuery : BaseFilterQuery
    {
        public bool? IsActive { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

