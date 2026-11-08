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
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeSummaryDto>> GetSummaryAsync();
        Task<BaseResponse<WashServiceResultDto>> CreateAsync(CreateWashServiceCommand command);
        Task<BaseResponse<WashServiceResultDto>> UpdateAsync(UpdateWashServiceCommand command);
        Task<BaseResponse<WashServiceResultDto>> UpdateStatusAsync(UpdateWashServiceStatusCommand command);
        Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null);
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeBatchResultDto>> BatchAsync(WashGo.Application.Contracts.Features.AuditContracts.Commands.AdministrativeBatchCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashServiceFilterQuery query);
    }
}
