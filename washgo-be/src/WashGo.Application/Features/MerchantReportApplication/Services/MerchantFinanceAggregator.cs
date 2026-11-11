using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.MerchantReportApplication.Services
{
    /// <summary>
    /// Tổng hợp tài chính của một Merchant tại DB theo ngày giao dịch: tiền giặt đã thu, tiền hoàn, phí đã thu và hoa hồng
    /// theo tỷ lệ chốt trên từng đơn (hoàn tiền giảm hoa hồng cùng tỷ lệ). Đơn chưa có tỷ lệ chốt (null) không được tính
    /// hoa hồng và được báo riêng; 0% là tỷ lệ hợp lệ. Không bao giờ dùng tỷ lệ hiện tại của Merchant cho dữ liệu cũ.
    /// Làm tròn theo CommissionMath (từng giao dịch, nửa đồng ra xa 0 = round(numeric) của PostgreSQL).
    /// Gọi trong transaction repeatable-read của bên gọi để các tổng cùng một snapshot.
    /// </summary>
    public class MerchantFinanceAggregator : ITransientDependency
    {
        private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<PaymentTransaction, Guid> _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IAsyncQueryableExecuter _executer;

        public MerchantFinanceAggregator(IWashOrderRepository orderRepo, IRepository<PaymentTransaction, Guid> paymentRepo,
            IRepository<RefundRequest, Guid> refundRepo, IAsyncQueryableExecuter executer)
        {
            _orderRepo = orderRepo;
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _executer = executer;
        }

        public sealed record Totals(decimal Gross, decimal Refunds, decimal Commission, decimal Fees, int PaidOrders,
            int UncoveredOrders, decimal UncoveredAmount)
        {
            public decimal Net => Gross - Refunds;
            public decimal Payout => Net - Commission;
            /// <summary>Hoa hồng đã tính đủ: mọi khoản thu/hoàn tiền giặt trong phạm vi đều có tỷ lệ chốt.</summary>
            public bool CommissionComplete => UncoveredOrders == 0;
        }

        public sealed record Bucket(DateTime LocalStart, decimal Gross, decimal Refunds, decimal Commission, int PaidOrders);

        // Member-init projection (not a constructor) so EF can keep filtering and grouping on it in SQL.
        private sealed class PayRow
        {
            public Guid OrderId { get; init; }
            public PaymentPurpose Purpose { get; init; }
            public decimal Amount { get; init; }
            public DateTime At { get; init; }
            public decimal? Rate { get; init; }
        }

        private async Task<IQueryable<PayRow>> PaymentsAsync(Guid merchantId, DateTime? from, DateTime? to)
        {
            var orders = await _orderRepo.GetQueryableAsync();
            var q = from p in await _paymentRepo.GetQueryableAsync()
                    join o in orders on p.OrderId equals o.Id
                    where (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded) &&
                          p.CompletedAt != null && (p.FinancialSnapshotBasis != null ? p.MerchantIdSnapshot : o.MerchantId) == merchantId
                    select new PayRow { OrderId = o.Id, Purpose = p.Purpose, Amount = p.Amount, At = p.CompletedAt!.Value,
                        Rate = p.FinancialSnapshotBasis != null ? p.CommissionRateSnapshot : o.CommissionRateApplied };
            if (from.HasValue) q = q.Where(x => x.At >= from.Value);
            if (to.HasValue) q = q.Where(x => x.At < to.Value);
            return q;
        }

        private async Task<IQueryable<PayRow>> RefundsAsync(Guid merchantId, DateTime? from, DateTime? to)
        {
            var orders = await _orderRepo.GetQueryableAsync();
            var q = from r in await _refundRepo.GetQueryableAsync()
                    join o in orders on r.OrderId equals o.Id
                    where r.Status == RefundRequestStatus.Refunded && r.RefundedAt != null &&
                        (r.FinancialSnapshotBasis != null ? r.MerchantIdSnapshot : o.MerchantId) == merchantId
                    select new PayRow { OrderId = o.Id, Purpose = PaymentPurpose.Order, Amount = r.Amount, At = r.RefundedAt!.Value,
                        Rate = r.FinancialSnapshotBasis != null ? r.CommissionRateSnapshot : o.CommissionRateApplied };
            if (from.HasValue) q = q.Where(x => x.At >= from.Value);
            if (to.HasValue) q = q.Where(x => x.At < to.Value);
            return q;
        }

        /// <summary>Totals over [from, to) (null = no bound), computed with SQL aggregates only.</summary>
        public async Task<Totals> TotalsAsync(Guid merchantId, DateTime? from, DateTime? to)
        {
            var pays = await PaymentsAsync(merchantId, from, to);
            var laundry = pays.Where(x => x.Purpose == PaymentPurpose.Order);
            var refunds = await RefundsAsync(merchantId, from, to);
            var gross = await _executer.SumAsync(laundry, x => (decimal?)x.Amount) ?? 0m;
            var fees = await _executer.SumAsync(pays.Where(x => x.Purpose != PaymentPurpose.Order), x => (decimal?)x.Amount) ?? 0m;
            var refunded = await _executer.SumAsync(refunds, x => (decimal?)x.Amount) ?? 0m;
            var commission = (await _executer.SumAsync(laundry.Where(x => x.Rate != null), x => (decimal?)Math.Round(x.Amount * x.Rate!.Value / 100m)) ?? 0m)
                             - (await _executer.SumAsync(refunds.Where(x => x.Rate != null), x => (decimal?)Math.Round(x.Amount * x.Rate!.Value / 100m)) ?? 0m);
            var paidOrders = await _executer.CountAsync(laundry.Select(x => x.OrderId).Distinct());
            // UNION is distinct in SQL: an order missing a rate on both a receipt and a refund counts once.
            var uncoveredIds = await _executer.CountAsync(laundry.Where(x => x.Rate == null).Select(x => x.OrderId)
                .Union(refunds.Where(x => x.Rate == null).Select(x => x.OrderId)));
            var uncoveredAmount = (await _executer.SumAsync(laundry.Where(x => x.Rate == null), x => (decimal?)x.Amount) ?? 0m)
                                  - (await _executer.SumAsync(refunds.Where(x => x.Rate == null), x => (decimal?)x.Amount) ?? 0m);
            return new Totals(gross, refunded, commission, fees, paidOrders, uncoveredIds, uncoveredAmount);
        }

        /// <summary>Per local (UTC+7) day or hour, grouped in SQL; empty periods are absent.</summary>
        public async Task<List<Bucket>> SeriesAsync(Guid merchantId, DateTime? from, DateTime? to, bool hourly)
        {
            var laundry = (await PaymentsAsync(merchantId, from, to)).Where(x => x.Purpose == PaymentPurpose.Order);
            var refunds = await RefundsAsync(merchantId, from, to);
            var pay = await _executer.ToListAsync(laundry
                .Select(x => new { Local = x.At.AddHours(7), x.Amount, x.OrderId, Commission = x.Rate == null ? 0m : Math.Round(x.Amount * x.Rate.Value / 100m) })
                .GroupBy(x => new { x.Local.Date, Hour = hourly ? x.Local.Hour : 0 })
                .Select(g => new { g.Key.Date, g.Key.Hour, Gross = g.Sum(x => x.Amount), Commission = g.Sum(x => x.Commission), Orders = g.Select(x => x.OrderId).Distinct().Count() }));
            var refund = await _executer.ToListAsync(refunds
                .Select(x => new { Local = x.At.AddHours(7), x.Amount, Commission = x.Rate == null ? 0m : Math.Round(x.Amount * x.Rate.Value / 100m) })
                .GroupBy(x => new { x.Local.Date, Hour = hourly ? x.Local.Hour : 0 })
                .Select(g => new { g.Key.Date, g.Key.Hour, Amount = g.Sum(x => x.Amount), Commission = g.Sum(x => x.Commission) }));
            var keys = pay.Select(x => x.Date.AddHours(x.Hour)).Concat(refund.Select(x => x.Date.AddHours(x.Hour))).Distinct().OrderBy(x => x);
            return keys.Select(k =>
            {
                var p = pay.FirstOrDefault(x => x.Date.AddHours(x.Hour) == k);
                var r = refund.FirstOrDefault(x => x.Date.AddHours(x.Hour) == k);
                return new Bucket(DateTime.SpecifyKind(k, DateTimeKind.Unspecified), p?.Gross ?? 0m, r?.Amount ?? 0m,
                    (p?.Commission ?? 0m) - (r?.Commission ?? 0m), p?.Orders ?? 0);
            }).ToList();
        }

        /// <summary>Local (UTC+7) wall-clock start of an instant, used to place series buckets.</summary>
        public static DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc + Vn, DateTimeKind.Unspecified);
    }
}
