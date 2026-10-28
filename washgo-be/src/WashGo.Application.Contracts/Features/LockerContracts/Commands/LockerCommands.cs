using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

namespace WashGo.Application.Contracts.Features.LockerContracts.Commands
{
    public class CreateLockerCommand
    {
        public Guid MerchantId { get; set; }
        public Guid? ServiceAreaId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Ward { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public LockerType LockerType { get; set; } = LockerType.Normal;
        public string? Description { get; set; }
        public List<CreateLockerBoxDto> Boxes { get; set; } = [];
    }

    public class UpdateLockerCommand : CreateLockerCommand
    {
        public Guid Id { get; set; }
        public LockerStatus Status { get; set; } = LockerStatus.Active;
    }

    public class CreateLockerBoxDto
    {
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize Size { get; set; } = LockerBoxSize.Standard;
    }

    public class UpdateLockerBoxStatusCommand
    {
        public Guid LockerId { get; set; }
        public Guid BoxId { get; set; }
        public LockerBoxStatus NewStatus { get; set; }
    }
}

