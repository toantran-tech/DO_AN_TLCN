using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.AccountContracts.Results;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AccountApplication.Services;

/// <summary>
/// Nghĩa vụ nghiệp vụ còn mở của một tài khoản. Xóa mềm bị chặn khi còn bất kỳ nghĩa vụ chặn nào; khóa dùng
/// một phần (Shipper còn nhiệm vụ → chặn; Customer/Merchant còn đơn → cần xác nhận). Chỉ đọc, không sửa trạng thái nghiệp vụ.
/// </summary>
public class AccountObligationReader(IWashOrderRepository orders, IRepository<RefundRequest, Guid> refunds,
    IRepository<Incident, Guid> incidents, IRepository<PaymentTransaction, Guid> payments,
    IRepository<MerchantSettlement, Guid> settlements, IShipperRepository shippers, IRepository<LockerMerchant, Guid> lockerLinks,
    ILockerRepository lockers, IRepository<LoyaltyWallet, Guid> wallets, IAsyncQueryableExecuter executer) : ITransientDependency
{
    private const int MaxItems = 20;

    /// <summary>Đơn chưa kết thúc: chưa Returned/Completed/Cancelled, hoặc đã hủy nhưng đồ còn ở locker/tiệm.</summary>
    public static IQueryable<WashOrder> Open(IQueryable<WashOrder> source) =>
        source.Where(o => !o.IsDeleted &&
            (o.Status != WashOrderStatus.Returned && o.Status != WashOrderStatus.Completed && o.Status != WashOrderStatus.Cancelled ||
             o.Status == WashOrderStatus.Cancelled && (o.CancelledGoodsStatus == CancelledGoodsStatus.AtDepositLocker ||
                                                       o.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant ||
                                                       o.CancelledGoodsStatus == CancelledGoodsStatus.AtReturnLocker)));

    public async Task<AccountObligationsDto> ReadAsync(Guid userId, string role, Guid? shipperId)
    {
        var result = new AccountObligationsDto();
        var orderQuery = await orders.GetQueryableAsync();
        switch (role)
        {
            case "Customer":
            {
                var open = Open(orderQuery).Where(o => o.CustomerId == userId);
                result.OpenOrders = await executer.CountAsync(open);
                result.Items = await ItemsAsync(open, "Order");
                var mine = orderQuery.Where(o => o.CustomerId == userId);
                result.PendingPayments = await executer.CountAsync((await payments.GetQueryableAsync())
                    .Where(p => (p.Status == PaymentTransactionStatus.Pending || p.Status == PaymentTransactionStatus.RequiresReview) &&
                                mine.Any(o => o.Id == p.OrderId)));
                result.OpenRefunds = await executer.CountAsync((await refunds.GetQueryableAsync())
                    .Where(r => r.CustomerId == userId && (r.Status == RefundRequestStatus.Pending || r.Status == RefundRequestStatus.Approved)));
                result.OpenIncidents = await executer.CountAsync((await incidents.GetQueryableAsync())
                    .Where(i => i.CustomerId == userId && (i.Status == IncidentStatus.Pending || i.CompensationStatus == CompensationStatus.Pending)));
                var wallet = await wallets.FindAsync(userId);
                result.LoyaltyPoints = wallet?.NetPoints ?? 0;
                result.HeldLoyaltyPoints = wallet?.HeldPoints ?? 0;
                Block(result, result.OpenOrders, "OpenOrders");
                Block(result, result.PendingPayments, "PendingPayments");
                Block(result, result.OpenRefunds, "OpenRefunds");
                Block(result, result.OpenIncidents, "OpenIncidents");
                Block(result, (int)Math.Min(int.MaxValue, result.HeldLoyaltyPoints), "HeldLoyaltyPoints");
                if (result.LoyaltyPoints < 0) result.DeleteBlockers.Add("LoyaltyDebt");
                break;
            }
            case "Merchant":
            {
                var open = Open(orderQuery).Where(o => o.MerchantId == userId);
                result.OpenOrders = await executer.CountAsync(open);
                result.Items = await ItemsAsync(open, "Order");
                var mine = orderQuery.Where(o => o.MerchantId == userId);
                result.ActiveShippers = await executer.CountAsync((await shippers.GetQueryableAsync()).Where(s => s.MerchantId == userId));
                result.LockerAssignments = await executer.CountAsync(from link in await lockerLinks.GetQueryableAsync()
                                                                      join locker in await lockers.GetQueryableAsync() on link.LockerId equals locker.Id
                                                                      where link.MerchantId == userId
                                                                      select link.Id);
                result.UnsettledSettlements = await executer.CountAsync((await settlements.GetQueryableAsync())
                    .Where(s => s.MerchantId == userId && s.Status == SettlementStatus.Pending));
                result.OpenRefunds = await executer.CountAsync((await refunds.GetQueryableAsync())
                    .Where(r => (r.Status == RefundRequestStatus.Pending || r.Status == RefundRequestStatus.Approved) && mine.Any(o => o.Id == r.OrderId)));
                result.OpenIncidents = await executer.CountAsync((await incidents.GetQueryableAsync())
                    .Where(i => i.MerchantId == userId && (i.Status == IncidentStatus.Pending || i.CompensationStatus == CompensationStatus.Pending)));
                Block(result, result.OpenOrders, "OpenOrders");
                Block(result, result.ActiveShippers, "ActiveShippers");
                Block(result, result.LockerAssignments, "LockerAssignments");
                Block(result, result.UnsettledSettlements, "UnsettledSettlements");
                Block(result, result.OpenRefunds, "OpenRefunds");
                Block(result, result.OpenIncidents, "OpenIncidents");
                break;
            }
            case "Shipper" when shipperId.HasValue:
            {
                var active = DeliveryTaskRules.ActiveDeliveries(orderQuery).Where(o => o.ShipperId == shipperId.Value);
                result.ActiveTasks = await executer.CountAsync(active);
                result.Items = await ItemsAsync(active, "Task");
                Block(result, result.ActiveTasks, "ActiveTasks");
                break;
            }
        }
        return result;
    }

    private async Task<List<AccountObligationItemDto>> ItemsAsync(IQueryable<WashOrder> source, string type) =>
        (await executer.ToListAsync(source.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(MaxItems)
            .Select(o => new { o.Id, o.OrderCode, o.Status })))
        .Select(o => new AccountObligationItemDto { Type = type, OrderId = o.Id, OrderCode = o.OrderCode, Status = o.Status }).ToList();

    private static void Block(AccountObligationsDto result, int count, string code)
    {
        if (count > 0) result.DeleteBlockers.Add(code);
    }
}
