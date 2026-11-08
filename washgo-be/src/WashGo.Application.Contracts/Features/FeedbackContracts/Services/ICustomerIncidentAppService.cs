using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Services
{
    /// <summary>Phần khiếu nại do Customer tự thao tác: lọc, điều kiện báo cáo, sửa/rút, trao đổi bổ sung minh chứng.</summary>
    public interface ICustomerIncidentAppService : IApplicationService
    {
        Task<PaginatedResult<IncidentResultDto>> FilterMineAsync(CustomerIncidentFilterQuery query);
        Task<BaseResponse<IncidentEligibilityDto>> GetEligibilityAsync(Guid orderId);
        Task<BaseResponse<IncidentResultDto>> GetByRequestIdAsync(string clientRequestId);
        Task<BaseResponse<IncidentResultDto>> UpdateAsync(Guid id, UpdateIncidentCommand command);
        Task<BaseResponse<IncidentResultDto>> WithdrawAsync(Guid id, WithdrawIncidentCommand command);
        Task<BaseResponse<List<IncidentMessageDto>>> GetMessagesAsync(Guid id);
        Task<PaginatedResult<IncidentMessageDto>> GetMessagePageAsync(Guid id, int page, int take);
        Task<BaseResponse<IncidentMessageDto>> AddMessageAsync(Guid id, AddIncidentMessageCommand command);
    }
}
