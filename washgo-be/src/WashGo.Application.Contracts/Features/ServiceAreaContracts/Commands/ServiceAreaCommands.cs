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
}

