using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Services
{
    public interface IRatingAppService : IApplicationService
    {
        Task<BaseResponse<RatingResultDto>> CreateAsync(CreateRatingCommand command);
        Task<BaseResponse<RatingResultDto?>> GetByOrderAsync(Guid orderId);
        Task<PaginatedResult<RatingResultDto>> GetListAsync(RatingFilterQuery query);
        Task<BaseResponse<RatingResultDto>> SetVisibilityAsync(Guid id, SetRatingVisibilityCommand command);
    }
}
