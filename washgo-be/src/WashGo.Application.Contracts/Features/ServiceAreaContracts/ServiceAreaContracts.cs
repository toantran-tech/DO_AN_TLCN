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

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries
{
    public class ServiceAreaFilterQuery : BaseFilterQuery
    {
        public bool? IsActive { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

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
        public long CreatedOn { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Services
{
    public interface IServiceAreaAppService : IApplicationService
    {
        Task<PaginatedResult<Results.ServiceAreaResultDto>> GetListAsync(Queries.ServiceAreaFilterQuery query);
        Task<BaseResponse<Results.ServiceAreaResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<Results.ServiceAreaResultDto>> CreateAsync(Commands.CreateServiceAreaCommand command);
        Task<BaseResponse<Results.ServiceAreaResultDto>> UpdateAsync(Commands.UpdateServiceAreaCommand command);
        Task<BaseResponse<bool>> ToggleActiveAsync(Guid id);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, Queries.ServiceAreaFilterQuery query);
    }
}
