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
        Task<BaseResponse<PromotionResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<PromotionResultDto>> CreateAsync(CreatePromotionCommand command);
        Task<BaseResponse<PromotionResultDto>> UpdateAsync(UpdatePromotionCommand command);
        Task<BaseResponse<bool>> DeleteAsync(Guid id);
        Task<BaseResponse<bool>> ToggleActiveAsync(Guid id);
        Task<BaseResponse<PromotionStatisticsDto>> GetStatisticsAsync(Guid id, long? fromDate, long? toDate);
        Task<BaseResponse<string>> ExportStatisticsCsvAsync(Guid id, long? fromDate, long? toDate);
    }
}
