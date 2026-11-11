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

        private sealed class Tx
        {
            public Guid OrderId { get; init; }
            public decimal Amount { get; init; }
            public decimal? Rate { get; init; }
            public Guid? SettlementId { get; init; }
            public bool Refund { get; init; }
        }

        /// <summary>
        /// Thu/hoàn từng đơn tính theo từng giao dịch với tỷ lệ chốt của chính giao dịch (CommissionMath). FinancialStatus:
        /// Unpaid; Collected (chưa vào kỳ đối soát đã chi trả); Settled (mọi khoản thu/hoàn đã thuộc kỳ đã chi trả);
        /// Adjusted (có khoản thu/hoàn phát sinh sau kỳ đã chi trả, chờ kỳ sau). Số đã/chưa đối soát trả riêng.
        /// </summary>
        public async Task FillAsync<T>(List<T> orders, string role) where T : WashOrderResultDto
        {
            if (orders.Count == 0 || role is not ("Admin" or "Merchant"))
                return;
            var ids = orders.Select(x => x.Id).ToList();
            var rateOf = orders.ToDictionary(x => x.Id, x => x.CommissionRateApplied);
            var txs = await _executer.ToListAsync((await _paymentRepo.GetQueryableAsync())
                .Where(p => ids.Contains(p.OrderId) && !p.IsDeleted && p.Purpose == PaymentPurpose.Order && p.CompletedAt != null &&
                            (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded))
                .Select(p => new Tx { OrderId = p.OrderId, Amount = p.Amount, SettlementId = p.SettlementId,
                    Rate = p.FinancialSnapshotBasis != null ? p.CommissionRateSnapshot : null }));
            txs.AddRange(await _executer.ToListAsync((await _refundRepo.GetQueryableAsync())
                .Where(r => ids.Contains(r.OrderId) && !r.IsDeleted && r.Status == RefundRequestStatus.Refunded)
                .Select(r => new Tx { OrderId = r.OrderId, Amount = r.Amount, SettlementId = r.SettlementId, Refund = true,
                    Rate = r.FinancialSnapshotBasis != null ? r.CommissionRateSnapshot : null })));
            var settlementIds = txs.Where(x => x.SettlementId.HasValue).Select(x => x.SettlementId!.Value).Distinct().ToList();
            var paidSettlements = (await _executer.ToListAsync((await _settlementRepo.GetQueryableAsync())
                .Where(s => settlementIds.Contains(s.Id) && !s.IsDeleted && s.Status == SettlementStatus.Paid).Select(s => s.Id))).ToHashSet();
            foreach (var order in orders)
            {
                var mine = txs.Where(x => x.OrderId == order.Id).ToList();
                decimal Signed(Tx x) => x.Refund ? -x.Amount : x.Amount;
                var collected = mine.Where(x => !x.Refund).Sum(x => x.Amount);
                var back = mine.Where(x => x.Refund).Sum(x => x.Amount);
                order.CollectedLaundryAmount = collected;
                order.RefundedAmount = back;
                // Transactions without their own snapshot use the rate locked on the order (legacy).
                var rates = mine.Select(x => x.Rate ?? rateOf[order.Id]).ToList();
                order.CommissionOnCollected = rates.All(r => r.HasValue)
                    ? mine.Sum(x => (x.Refund ? -1 : 1) * CommissionMath.Of(x.Amount, (x.Rate ?? rateOf[order.Id])!.Value)) : null;
                if (mine.Count == 0 && order.CommissionRateApplied.HasValue) order.CommissionOnCollected = 0m;
                order.MerchantPayoutOnCollected = order.CommissionOnCollected.HasValue ? collected - back - order.CommissionOnCollected.Value : null;
                var settled = mine.Where(x => x.SettlementId.HasValue && paidSettlements.Contains(x.SettlementId.Value)).ToList();
                order.SettledNetAmount = settled.Sum(Signed);
                order.UnsettledNetAmount = mine.Except(settled).Sum(Signed);
                order.FinancialStatus = collected == 0m ? "Unpaid"
                    : settled.Count == mine.Count ? "Settled"
                    : settled.Count > 0 ? "Adjusted"
                    : back > 0m ? "Adjusted" : "Collected";
            }
        }
    }
}
