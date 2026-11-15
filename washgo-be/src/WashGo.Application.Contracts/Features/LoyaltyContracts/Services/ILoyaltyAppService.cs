using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.LoyaltyContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.LoyaltyContracts.Services;

public interface ILoyaltyAppService : IApplicationService
{
    Task<BaseResponse<LoyaltyBalanceDto>> GetBalanceAsync();
    Task<PaginatedResult<LoyaltyEntryDto>> GetHistoryAsync(int page, int take);
    Task<BaseResponse<LoyaltyQuoteDto>> QuoteAsync(Guid orderId, long pointsToRedeem);
}
