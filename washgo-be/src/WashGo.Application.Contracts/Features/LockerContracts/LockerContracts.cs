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

namespace WashGo.Application.Contracts.Features.LockerContracts.Queries
{
    public class LockerFilterQuery : BaseFilterQuery
    {
        public LockerStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.LockerContracts.Results
{
    public class LockerResultDto
    {
        public Guid Id { get; set; }
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
        public LockerStatus Status { get; set; }
        public LockerType LockerType { get; set; }
        public int TotalBoxes { get; set; }
        public int AvailableBoxes { get; set; }
        public int EmptyBoxesCount { get; set; }
        public long CreatedOn { get; set; }
        public List<LockerBoxResultDto> Boxes { get; set; } = [];
    }

    public class LockerBoxResultDto
    {
        public Guid Id { get; set; }
        public Guid LockerId { get; set; }
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize Size { get; set; }
        public LockerBoxStatus Status { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.LockerContracts.Services
{
    public interface ILockerAppService : IApplicationService
    {
        System.Threading.Tasks.Task<PaginatedResult<Results.LockerResultDto>> GetListAsync(Queries.LockerFilterQuery query);
        System.Threading.Tasks.Task<BaseResponse<Results.LockerResultDto>> GetByIdAsync(Guid id);
        System.Threading.Tasks.Task<BaseResponse<Results.LockerResultDto>> CreateAsync(Commands.CreateLockerCommand command);
        System.Threading.Tasks.Task<BaseResponse<Results.LockerResultDto>> UpdateAsync(Commands.UpdateLockerCommand command);
        System.Threading.Tasks.Task<BaseResponse<bool>> UpdateBoxStatusAsync(Commands.UpdateLockerBoxStatusCommand command);
        System.Threading.Tasks.Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, Queries.LockerFilterQuery query);
    }
}