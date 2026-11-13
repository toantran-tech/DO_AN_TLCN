using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using System.Threading.Tasks;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;

namespace WashGo.Application.Contracts.Features.LockerContracts.Services
{
    public interface ILockerAppService : IApplicationService
    {
        Task<PaginatedResult<LockerResultDto>> GetListAsync(LockerFilterQuery query);
        Task<BaseResponse<LockerResultDto>> GetByIdAsync(Guid id);
        Task<PaginatedResult<LockerUsageDto>> GetUsageHistoryAsync(Guid id, LockerUsageQuery query);
        Task<BaseResponse<LockerSummaryDto>> GetSummaryAsync(Guid? merchantId);
        Task<BaseResponse<LockerResultDto>> CreateAsync(CreateLockerCommand command);
        Task<BaseResponse<LockerResultDto>> UpdateAsync(UpdateLockerCommand command);
        Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command);
        Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null);
        Task<BaseResponse<LockerResultDto>> SetStatusAsync(Guid id, SetLockerStatusCommand command);
        Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, LockerFilterQuery query);
    }
}
