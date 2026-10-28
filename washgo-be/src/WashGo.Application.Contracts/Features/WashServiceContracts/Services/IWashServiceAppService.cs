using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Services
{
    public interface IWashServiceAppService : IApplicationService
    {
        Task<PaginatedResult<WashServiceResultDto>> GetListAsync(WashServiceFilterQuery query);
        Task<BaseResponse<WashServiceResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<WashServiceResultDto>> CreateAsync(CreateWashServiceCommand command);
        Task<BaseResponse<WashServiceResultDto>> UpdateAsync(UpdateWashServiceCommand command);
        Task<BaseResponse<WashServiceResultDto>> UpdateStatusAsync(UpdateWashServiceStatusCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashServiceFilterQuery query);
    }
}
