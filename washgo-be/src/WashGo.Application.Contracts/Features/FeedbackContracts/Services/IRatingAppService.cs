using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Services
{
    public interface IRatingAppService : IApplicationService
    {
        Task<BaseResponse<CustomerRatingDto>> CreateAsync(CreateRatingCommand command);
        Task<BaseResponse<CustomerRatingDto?>> GetByOrderAsync(Guid orderId);
        Task<BaseResponse<AdministrativeOperationDto>> GetCreateOperationAsync(Guid operationId);
        Task<PaginatedResult<RatingResultDto>> GetListAsync(RatingFilterQuery query);
        Task<PaginatedResult<PartnerRatingDto>> GetPartnerListAsync(RatingFilterQuery query);
        Task<BaseResponse<RatingResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<RatingSummaryDto>> GetSummaryAsync(RatingFilterQuery query);
        Task<BaseResponse<RatingResultDto>> SetVisibilityAsync(Guid id, SetRatingVisibilityCommand command);
        Task<BaseResponse<RatingResultDto>> PublishAsync(Guid id, PublishRatingCommand command);
        Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(RatingBatchCommand command);
    }
}
