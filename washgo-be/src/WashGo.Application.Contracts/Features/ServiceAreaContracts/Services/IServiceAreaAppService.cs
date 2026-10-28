using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;

namespace WashGo.Application.Contracts.Features.ServiceAreaContracts.Services
{
    public interface IServiceAreaAppService : IApplicationService
    {
        Task<PaginatedResult<ServiceAreaResultDto>> GetListAsync(ServiceAreaFilterQuery query);
        Task<BaseResponse<ServiceAreaResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<ServiceAreaResultDto>> CreateAsync(CreateServiceAreaCommand command);
        Task<BaseResponse<ServiceAreaResultDto>> UpdateAsync(UpdateServiceAreaCommand command);
        Task<BaseResponse<bool>> ToggleActiveAsync(Guid id);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, ServiceAreaFilterQuery query);
    }
}
