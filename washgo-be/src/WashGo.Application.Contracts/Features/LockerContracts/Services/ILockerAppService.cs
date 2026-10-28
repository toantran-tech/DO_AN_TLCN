using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Results;

namespace WashGo.Application.Contracts.Features.LockerContracts.Services
{
    public interface ILockerAppService : IApplicationService
    {
        System.Threading.Tasks.Task<PaginatedResult<LockerResultDto>> GetListAsync(LockerFilterQuery query);
        System.Threading.Tasks.Task<BaseResponse<LockerResultDto>> GetByIdAsync(Guid id);
        System.Threading.Tasks.Task<BaseResponse<LockerResultDto>> CreateAsync(CreateLockerCommand command);
        System.Threading.Tasks.Task<BaseResponse<LockerResultDto>> UpdateAsync(UpdateLockerCommand command);
        System.Threading.Tasks.Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command);
        System.Threading.Tasks.Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, LockerFilterQuery query);
    }
}
