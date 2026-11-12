using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FinanceContracts.Commands;
using WashGo.Application.Contracts.Features.FinanceContracts.Queries;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Services
{
    public interface ICommissionReviewAppService : IApplicationService
    {
        Task<BaseResponse<CommissionReviewPageDto>> GetReviewAsync(CommissionReviewQuery query);
        Task<BaseResponse<CommissionReviewItemDto>> ReconcileAsync(Guid orderId, ReconcileCommissionCommand command);
    }
}
