using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Commands;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Queries;
using WashGo.Application.Contracts.Features.OrderAssignmentContracts.Results;

namespace WashGo.Application.Contracts.Features.OrderAssignmentContracts.Services
{
    public interface IOrderAssignmentAppService : IApplicationService
    {
        Task<PaginatedResult<WaitingOrderDto>> GetWaitingOrdersAsync(WaitingOrderFilterQuery query);
        Task<BaseResponse<OrderCandidatesDto>> GetCandidatesAsync(Guid orderId);
        Task<BaseResponse<AssignmentResultDto>> AssignAsync(Guid orderId, AssignOrderCommand command);
        Task<BaseResponse<List<AutoSuggestionDto>>> SuggestAsync(AutoSuggestCommand command);
        Task<BaseResponse<List<AssignmentResultDto>>> AutoAssignAsync(AutoAssignCommand command);
        Task<BaseResponse<AssignmentOperationDto>> GetOperationAsync(Guid operationId);
    }
}
