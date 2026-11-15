using System.Net;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.LoyaltyContracts.Results;
using WashGo.Application.Contracts.Features.LoyaltyContracts.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LoyaltyApplication.Services;

public class LoyaltyAppService(ILoyaltyWalletStore wallets, IRepository<LoyaltyEntry, Guid> entries,
    IWashOrderRepository orders, SystemSettingProvider settings) : WashGoAppService, ILoyaltyAppService
{
    private Guid CustomerId => CurrentUser.IsInRole("Customer") && CurrentUser.Id.HasValue
        ? CurrentUser.Id.Value : throw new AbpAuthorizationException();

    public async Task<BaseResponse<LoyaltyBalanceDto>> GetBalanceAsync()
    {
        var id = CustomerId;
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var policy = await settings.GetAsync();
        var wallet = await wallets.LockAsync(id);
        var dto = new LoyaltyBalanceDto
        {
            TotalPoints = Math.Max(0, wallet.NetPoints), HeldPoints = wallet.HeldPoints,
            AvailablePoints = LoyaltyMath.Available(wallet.NetPoints, wallet.HeldPoints),
            DebtPoints = Math.Max(0, -wallet.NetPoints), EarnPercent = policy.LoyaltyEarnPercent,
            MaxUsePercent = policy.LoyaltyMaxUsePercent
        };
        await uow.CompleteAsync();
        return Success(dto);
    }

    public async Task<PaginatedResult<LoyaltyEntryDto>> GetHistoryAsync(int page, int take)
    {
        var id = CustomerId;
        if (page is < 1 or > 100000 || take is < 1 or > 100)
            return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "page từ 1–100000, take từ 1–100." };
        var query = (await entries.GetQueryableAsync()).Where(x => x.CustomerId == id);
        var count = await AsyncExecuter.LongCountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * take).Take(take).Select(x => new LoyaltyEntryDto
            {
                Id = x.Id, OrderId = x.OrderId, Kind = x.Kind, PointsDelta = x.PointsDelta,
                HeldDelta = x.HeldDelta, NetPointsAfter = x.BalanceAfter, HeldPointsAfter = x.HeldAfter, CreatedAt = x.CreatedAt
            }));
        return Success(rows, count, page, take);
    }

    public async Task<BaseResponse<LoyaltyQuoteDto>> QuoteAsync(Guid orderId, long pointsToRedeem)
    {
        var id = CustomerId;
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var order = await orders.GetForUpdateAsync(orderId);
        if (order == null || order.CustomerId != id) return NotFound<LoyaltyQuoteDto>("Không tìm thấy đơn hàng của bạn.");
        if (order.PaymentStatus != PaymentStatus.Unpaid || order.Status is not (WashOrderStatus.AwaitingPayment or WashOrderStatus.MovedToStorage) ||
            order.FinalAmount <= 0 || order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue)
            return BadRequest<LoyaltyQuoteDto>("Đơn chưa sẵn sàng thanh toán tiền giặt.");
        var policy = await settings.GetAsync();
        var wallet = await wallets.LockAsync(id);
        var available = LoyaltyMath.Available(wallet.NetPoints, wallet.HeldPoints);
        var maximum = LoyaltyMath.MaximumUse(order.FinalAmount, policy.LoyaltyMaxUsePercent, available);
        if (pointsToRedeem < 0 || pointsToRedeem > maximum)
            return BadRequest<LoyaltyQuoteDto>($"Số điểm sử dụng phải từ 0 đến {maximum}.");
        var dto = new LoyaltyQuoteDto
        {
            OrderId = order.Id, LaundryAmount = order.FinalAmount, AvailablePoints = available,
            MaximumPoints = maximum, PointsToRedeem = pointsToRedeem, CashAmount = order.FinalAmount - pointsToRedeem,
            ExpectedEarnedPoints = LoyaltyMath.Earn(order.FinalAmount - pointsToRedeem, policy.LoyaltyEarnPercent)
        };
        await uow.CompleteAsync();
        return Success(dto, "Báo giá tham khảo; BE kiểm tra lại số dư khi tạo phiên thanh toán. Phiên đang giữ điểm phải tiếp tục dùng cùng số điểm.");
    }
}
