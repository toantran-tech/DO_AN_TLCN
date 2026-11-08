using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.PromotionContracts.Commands;
using WashGo.Application.Contracts.Features.PromotionContracts.Queries;
using WashGo.Application.Contracts.Features.PromotionContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.PromotionContracts.Services
{
    public interface IPromotionAppService : IApplicationService
    {
        Task<PaginatedResult<PromotionResultDto>> GetListAsync(PromotionFilterQuery query);
        Task<PaginatedResult<PromotionUsageDto>> GetUsageHistoryAsync(Guid id, PromotionUsageFilterQuery query);
        Task<BaseResponse<PromotionResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeSummaryDto>> GetSummaryAsync();
        Task<BaseResponse<PromotionResultDto>> CreateAsync(CreatePromotionCommand command);
        Task<BaseResponse<PromotionResultDto>> UpdateAsync(UpdatePromotionCommand command);
        Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null);
        Task<BaseResponse<PromotionResultDto>> SetStatusAsync(Guid id, SetPromotionStatusCommand command);
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeBatchResultDto>> BatchAsync(WashGo.Application.Contracts.Features.AuditContracts.Commands.AdministrativeBatchCommand command);
        Task<BaseResponse<bool>> ToggleActiveAsync(Guid id);
        Task<BaseResponse<PromotionStatisticsDto>> GetStatisticsAsync(Guid id, long? fromDate, long? toDate);
        Task<BaseResponse<string>> ExportStatisticsCsvAsync(Guid id, long? fromDate, long? toDate);
    }
}
