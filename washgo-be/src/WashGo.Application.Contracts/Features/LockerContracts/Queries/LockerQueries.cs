using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

namespace WashGo.Application.Contracts.Features.LockerContracts.Queries
{
    public class LockerFilterQuery : BaseFilterQuery
    {
        public LockerStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

