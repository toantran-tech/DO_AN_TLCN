using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>Số đã thu/hoàn và trạng thái tài chính từng đơn cho Admin/Merchant, nạp theo lô cho cả trang.</summary>
    public class OrderFinanceEnricher : ITransientDependency
    {
        private readonly IRepository<PaymentTransaction, Guid> _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IRepository<MerchantSettlement, Guid> _settlementRepo;
        private readonly IAsyncQueryableExecuter _executer;

        public OrderFinanceEnricher(IRepository<PaymentTransaction, Guid> paymentRepo, IRepository<RefundRequest, Guid> refundRepo,
            IRepository<MerchantSettlement, Guid> settlementRepo, IAsyncQueryableExecuter executer)
        {
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _settlementRepo = settlementRepo;
            _executer = executer;
        }

        public async Task FillAsync<T>(List<T> orders, string role) where T : WashOrderResultDto
        {
            if (orders.Count == 0 || role is not ("Admin" or "Merchant"))
                return;
            var ids = orders.Select(x => x.Id).ToList();
            var paid = (await _executer.ToListAsync((await _paymentRepo.GetQueryableAsync())
                    .Where(p => ids.Contains(p.OrderId) && p.Purpose == PaymentPurpose.Order && p.CompletedAt != null &&
                                (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded))
                    .GroupBy(p => p.OrderId).Select(g => new { OrderId = g.Key, Amount = g.Sum(x => x.Amount), Last = g.Max(x => x.CompletedAt) })))
                .ToDictionary(x => x.OrderId);
            var refunded = (await _executer.ToListAsync((await _refundRepo.GetQueryableAsync())
                    .Where(r => ids.Contains(r.OrderId) && r.Status == RefundRequestStatus.Refunded)
                    .GroupBy(r => r.OrderId).Select(g => new { OrderId = g.Key, Amount = g.Sum(x => x.Amount) })))
                .ToDictionary(x => x.OrderId, x => x.Amount);
            var merchantIds = orders.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value).Distinct().ToList();
            var settled = await _executer.ToListAsync((await _settlementRepo.GetQueryableAsync())
                .Where(s => !s.IsDeleted && s.Status == SettlementStatus.Paid && merchantIds.Contains(s.MerchantId))
                .Select(s => new { s.MerchantId, s.PeriodFrom, s.PeriodTo }));
            foreach (var order in orders)
            {
                var collected = paid.TryGetValue(order.Id, out var p) ? p.Amount : 0m;
                var back = refunded.GetValueOrDefault(order.Id);
                order.CollectedLaundryAmount = collected;
                order.RefundedAmount = back;
                var net = collected - back;
                order.CommissionOnCollected = order.CommissionRateApplied.HasValue
                    ? decimal.Round(net * order.CommissionRateApplied.Value / 100m, 0, MidpointRounding.AwayFromZero) : null;
                order.MerchantPayoutOnCollected = order.CommissionOnCollected.HasValue ? net - order.CommissionOnCollected.Value : null;
                order.FinancialStatus = collected == 0m ? "Unpaid"
                    : p?.Last is { } last && settled.Any(s => s.MerchantId == order.MerchantId && s.PeriodFrom <= last && last <= s.PeriodTo) ? "Settled"
                    : back > 0m ? "Adjusted" : "Collected";
            }
        }
    }
}
