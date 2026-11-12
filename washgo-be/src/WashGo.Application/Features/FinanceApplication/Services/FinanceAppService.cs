using WashGo.Domain.Shared.Validation;
using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FinanceContracts.Commands;
using WashGo.Application.Contracts.Features.FinanceContracts.Queries;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Application.Contracts.Features.FinanceContracts.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.Payment.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FinanceApplication.Services
{
    /// <summary>
    /// UC 3.8 Quản lý tài chính. Doanh thu ghi nhận theo giao dịch thanh toán thành công
    /// (kể cả giao dịch sau đó được hoàn) tại thời điểm hoàn tất; tiền đã hoàn trong kỳ được trừ ra.
    /// Hoa hồng chỉ tính trên tiền giặt (phí quá hạn/lưu kho/hủy thuộc nền tảng, không chia cho Merchant).
    /// </summary>
    public class FinanceAppService : WashGoAppService, IFinanceAppService
    {
        private const int MaxPeriodDays = 366;
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        private readonly IPaymentTransactionRepository _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<MerchantSettlement, Guid> _settlementRepo;
        private readonly NotificationPublisher _notifications;

        public FinanceAppService(
            IPaymentTransactionRepository paymentRepo,
            IRepository<RefundRequest, Guid> refundRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderItem, Guid> itemRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<MerchantSettlement, Guid> settlementRepo,
            NotificationPublisher notifications)
        {
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _orderRepo = orderRepo;
            _itemRepo = itemRepo;
            _merchantRepo = merchantRepo;
            _settlementRepo = settlementRepo;
            _notifications = notifications;
        }

        private sealed record PaymentRow(Guid Id, Guid OrderId, Guid? MerchantId, PaymentPurpose Purpose, decimal Amount, DateTime CompletedAt, decimal? CommissionRateApplied);
        private sealed record RefundRow(Guid Id, Guid OrderId, Guid? MerchantId, decimal Amount, DateTime RefundedAt, decimal? CommissionRateApplied);

        private static (DateTime From, DateTime To, string? Error) Period(long? fromDate, long? toDate)
        {
            if (fromDate is < 0 or > 253402300799 || toDate is < 0 or > 253402300799)
                return (DateTime.UnixEpoch, DateTime.UnixEpoch, "Thời gian Unix không hợp lệ.");
            var to = toDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(toDate.Value).UtcDateTime : DateTime.UtcNow;
            var from = fromDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromDate.Value).UtcDateTime : to.AddDays(-30);
            if (from > to)
                return (from, to, "Thời gian bắt đầu phải trước thời gian kết thúc.");
            if ((to - from).TotalDays > MaxPeriodDays)
                return (from, to, $"Khoảng thời gian tối đa {MaxPeriodDays} ngày.");
            return (from, to, null);
        }

        private async Task<List<PaymentRow>> PaymentsAsync(DateTime periodFrom, DateTime periodTo, Guid? merchantId, bool unsettledOnly = false)
        {
            var payments = await _paymentRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var query = from p in payments
                        join o in orders on p.OrderId equals o.Id
                        where !p.IsDeleted && (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded)
                              && p.CompletedAt != null && (unsettledOnly ? p.SettlementId == null : p.CompletedAt >= periodFrom) && p.CompletedAt <= periodTo
                              && (merchantId == null || (p.FinancialSnapshotBasis != null ? p.MerchantIdSnapshot : o.MerchantId) == merchantId)
                        select new PaymentRow(p.Id, o.Id, p.FinancialSnapshotBasis != null ? p.MerchantIdSnapshot : o.MerchantId, p.Purpose, p.Amount, p.CompletedAt!.Value,
                            p.FinancialSnapshotBasis != null ? p.CommissionRateSnapshot : o.CommissionRateApplied);
            var rows = await AsyncExecuter.ToListAsync(query.Take(FinancialReportLimitException.MaximumSourceRows + 1));
            if (rows.Count > FinancialReportLimitException.MaximumSourceRows) throw new FinancialReportLimitException();
            return rows;
        }

        private async Task<List<RefundRow>> RefundsAsync(DateTime periodFrom, DateTime periodTo, Guid? merchantId, bool unsettledOnly = false)
        {
            var refunds = await _refundRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var query = from r in refunds
                        join o in orders on r.OrderId equals o.Id
                        where !r.IsDeleted && r.Status == RefundRequestStatus.Refunded && r.RefundedAt != null
                              && (unsettledOnly ? r.SettlementId == null : r.RefundedAt >= periodFrom) && r.RefundedAt <= periodTo
                              && (merchantId == null || (r.FinancialSnapshotBasis != null ? r.MerchantIdSnapshot : o.MerchantId) == merchantId)
                        select new RefundRow(r.Id, o.Id, r.FinancialSnapshotBasis != null ? r.MerchantIdSnapshot : o.MerchantId, r.Amount, r.RefundedAt!.Value,
                            r.FinancialSnapshotBasis != null ? r.CommissionRateSnapshot : o.CommissionRateApplied);
            var rows = await AsyncExecuter.ToListAsync(query.Take(FinancialReportLimitException.MaximumSourceRows + 1));
            if (rows.Count > FinancialReportLimitException.MaximumSourceRows) throw new FinancialReportLimitException();
            return rows;
        }

        private async Task<Dictionary<Guid, Merchant>> MerchantsAsync(IEnumerable<Guid?> ids)
        {
            var list = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            return (await _merchantRepo.GetListAsync(x => list.Contains(x.Id))).ToDictionary(x => x.Id);
        }

        public async Task<BaseResponse<RevenueReportDto>> GetRevenueReportAsync(FinancePeriodQuery query)
        {
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var (from, to, error) = Period(query.FromDate, query.ToDate);
            if (error != null)
                return BadRequest<RevenueReportDto>(error);
            var hourly = string.Equals(query.Granularity?.Trim(), "hour", StringComparison.OrdinalIgnoreCase);
            if (query.Granularity != null && !hourly && !string.Equals(query.Granularity.Trim(), "day", StringComparison.OrdinalIgnoreCase))
                return BadRequest<RevenueReportDto>("granularity chỉ nhận day hoặc hour.");
            if (hourly && (to - from).TotalHours > 48)
                return BadRequest<RevenueReportDto>("Biểu đồ theo giờ chỉ áp dụng cho kỳ tối đa 48 giờ.");
            if (query.AsOf is < 0 or > 253402300799) return BadRequest<RevenueReportDto>("asOf không hợp lệ.");
            if (query.AsOf is > 0 && DateTimeOffset.FromUnixTimeSeconds(query.AsOf.Value).UtcDateTime < to)
                to = DateTimeOffset.FromUnixTimeSeconds(query.AsOf.Value).UtcDateTime;
            var payments = await PaymentsAsync(from, to, query.MerchantId);
            var refunds = await RefundsAsync(from, to, query.MerchantId);
            var merchants = await MerchantsAsync(payments.Select(x => x.MerchantId).Concat(refunds.Select(x => x.MerchantId)));

            var laundryOrderIds = payments.Where(x => x.Purpose == PaymentPurpose.Order).Select(x => x.OrderId).Distinct().ToList();
            var items = await AsyncExecuter.ToListAsync((await _itemRepo.GetQueryableAsync()).Where(x => laundryOrderIds.Contains(x.WashOrderId)).Take(FinancialReportLimitException.MaximumSourceRows + 1));
            if (items.Count > FinancialReportLimitException.MaximumSourceRows) throw new FinancialReportLimitException();

            var report = new RevenueReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                FromDate = from,
                ToDate = to,
                LaundryRevenue = payments.Where(x => x.Purpose == PaymentPurpose.Order).Sum(x => x.Amount),
                StorageFeeRevenue = payments.Where(x => x.Purpose == PaymentPurpose.OverdueStorageFee).Sum(x => x.Amount),
                CancellationFeeRevenue = payments.Where(x => x.Purpose == PaymentPurpose.CancellationFee).Sum(x => x.Amount),
                RefundedAmount = refunds.Sum(x => x.Amount),
                PaidOrderCount = laundryOrderIds.Count,
                ByMerchant = payments.Select(x => x.MerchantId).Concat(refunds.Select(x => x.MerchantId)).Distinct()
                    .Select(id => new MerchantRevenueDto
                    {
                        MerchantId = id,
                        MerchantName = id.HasValue && merchants.TryGetValue(id.Value, out var m) ? m.BusinessName : "(chưa gán Merchant)",
                        Revenue = payments.Where(x => x.MerchantId == id).Sum(x => x.Amount) -
                                  refunds.Where(x => x.MerchantId == id).Sum(x => x.Amount),
                        PaidOrderCount = payments.Where(x => x.MerchantId == id && x.Purpose == PaymentPurpose.Order)
                            .Select(x => x.OrderId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.Revenue).ToList(),
                ByService = items.GroupBy(x => x.ServiceId)
                    .Select(g => new ServiceRevenueDto
                    {
                        ServiceId = g.Key,
                        ServiceName = g.First().ServiceName,
                        Amount = g.Sum(x => x.SubTotal),
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .OrderByDescending(x => x.Amount).ToList(),
                Daily = payments.Select(x => (Date: (x.CompletedAt + VietnamOffset).Date, x.Amount))
                    .Concat(refunds.Select(x => (Date: (x.RefundedAt + VietnamOffset).Date, Amount: -x.Amount)))
                    .GroupBy(x => x.Date).OrderBy(g => g.Key)
                    .Select(g => new DailyRevenueDto
                    {
                        Date = g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Revenue = g.Sum(x => x.Amount)
                    }).ToList()
            };
            report.TotalRevenue = report.LaundryRevenue + report.StorageFeeRevenue + report.CancellationFeeRevenue - report.RefundedAmount;
            report.AsOf = DateTime.SpecifyKind(to, DateTimeKind.Utc);
            if (hourly)
                report.Hourly = payments.Select(x => (Hour: Hour(x.CompletedAt + VietnamOffset), x.Amount))
                    .Concat(refunds.Select(x => (Hour: Hour(x.RefundedAt + VietnamOffset), Amount: -x.Amount)))
                    .GroupBy(x => x.Hour).OrderBy(g => g.Key)
                    .Select(g => new HourlyRevenueDto { HourStart = g.Key.ToString("yyyy-MM-ddTHH:00:00", CultureInfo.InvariantCulture) + "+07:00", Revenue = g.Sum(x => x.Amount) })
                    .ToList();
            var empty = payments.Count == 0 && refunds.Count == 0;
            await snapshot.CompleteAsync();
            return Success(report, empty ? "Không có dữ liệu trong khoảng thời gian này." : null);
        }

        public async Task<BaseResponse<string>> ExportRevenueReportCsvAsync(FinancePeriodQuery query)
        {
            var result = await GetRevenueReportAsync(query);
            if (!result.Succeeded || result.Data == null)
                return BadRequest<string>(result.Message ?? "Không thể xuất báo cáo. Vui lòng thử lại.");
            var r = result.Data;
            static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
            static string Q(string v) => "\"" + ((v.TrimStart().StartsWith('=') || v.TrimStart().StartsWith('+') || v.TrimStart().StartsWith('-') || v.TrimStart().StartsWith('@') || v.StartsWith('\t') || v.StartsWith('\r')) ? "'" : "") + v.Replace("\"", "\"\"") + "\"";
            var csv = new StringBuilder();
            csv.AppendLine("Muc,Gia tri");
            csv.AppendLine($"GeneratedAtUtc,{r.GeneratedAt:O}");
            csv.AppendLine("Timezone,Asia/Ho_Chi_Minh");
            csv.AppendLine("Currency,VND");
            // Bounds are printed in Vietnam time with an explicit offset (never a UTC wall clock under a VN label).
            static string Vn(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(VietnamOffset).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
            csv.AppendLine($"Tu ngay,{Vn(r.FromDate)}");
            csv.AppendLine($"Den ngay,{Vn(r.ToDate)}");
            csv.AppendLine($"Du lieu den (asOf),{Vn(r.AsOf)}");
            csv.AppendLine("Ghi chu,Ban tinh tai thoi diem xuat; truyen asOf = generatedAt cua bao cao dang xem de khop du lieu");
            csv.AppendLine($"Tong doanh thu,{N(r.TotalRevenue)}");
            csv.AppendLine($"Tien giat,{N(r.LaundryRevenue)}");
            csv.AppendLine($"Phi qua han/luu kho,{N(r.StorageFeeRevenue)}");
            csv.AppendLine($"Phi huy,{N(r.CancellationFeeRevenue)}");
            csv.AppendLine($"Da hoan,{N(r.RefundedAmount)}");
            csv.AppendLine();
            csv.AppendLine("Merchant,So don da thanh toan,Doanh thu");
            foreach (var m in r.ByMerchant)
                csv.AppendLine($"{Q(m.MerchantName)},{m.PaidOrderCount},{N(m.Revenue)}");
            csv.AppendLine();
            csv.AppendLine("Dich vu,So luong,Thanh tien");
            foreach (var s in r.ByService)
                csv.AppendLine($"{Q(s.ServiceName)},{s.Quantity},{N(s.Amount)}");
            csv.AppendLine();
            csv.AppendLine("Ngay,Doanh thu");
            foreach (var d in r.Daily)
                csv.AppendLine($"{d.Date},{N(d.Revenue)}");
            return Success(csv.ToString());
        }

        private async Task<List<MerchantCommissionDto>> BuildCommissionsAsync(DateTime from, DateTime to, Guid? merchantId, bool unsettledOnly = false)
        {
            var payments = (await PaymentsAsync(from, to, merchantId, unsettledOnly)).Where(x => x.Purpose == PaymentPurpose.Order && x.MerchantId != null).ToList();
            var refunds = (await RefundsAsync(from, to, merchantId, unsettledOnly)).Where(x => x.MerchantId != null).ToList();
            var merchantIds = payments.Select(x => x.MerchantId).Concat(refunds.Select(x => x.MerchantId)).Distinct().ToList();
            if (merchantId.HasValue && !merchantIds.Contains(merchantId))
                merchantIds.Add(merchantId);
            var merchants = await MerchantsAsync(merchantIds);
            return merchantIds.Where(id => merchants.ContainsKey(id!.Value)).Select(id =>
            {
                var merchant = merchants[id!.Value];
                var mPayments = payments.Where(x => x.MerchantId == id).ToList();
                var mRefunds = refunds.Where(x => x.MerchantId == id).ToList();
                var gross = mPayments.Sum(x => x.Amount);
                // Refunds in this period may reverse receipts from an earlier period: retain the negative adjustment.
                var refunded = mRefunds.Sum(x => x.Amount);
                var net = gross - refunded;

                // Commission per order at the rate locked on that order (0% is a valid rate). Orders without a locked
                // rate are not guessed from the Merchant's current rate: they are counted as needing reconciliation.
                var orderIds = mPayments.Select(x => x.OrderId).Concat(mRefunds.Select(x => x.OrderId)).Distinct().ToList();
                decimal totalCommission = 0m;
                var uncovered = 0;
                decimal uncoveredNet = 0m;
                foreach (var oId in orderIds)
                {
                    var oPays = mPayments.Where(x => x.OrderId == oId).ToList();
                    var oRefs = mRefunds.Where(x => x.OrderId == oId).ToList();
                    // Same rule as the Merchant report: per transaction at its own locked rate (CommissionMath).
                    if (oPays.Any(x => x.CommissionRateApplied == null) || oRefs.Any(x => x.CommissionRateApplied == null))
                    {
                        uncovered++;
                        uncoveredNet += oPays.Where(x => x.CommissionRateApplied == null).Sum(x => x.Amount)
                                        - oRefs.Where(x => x.CommissionRateApplied == null).Sum(x => x.Amount);
                    }
                    totalCommission += oPays.Where(x => x.CommissionRateApplied != null).Sum(x => CommissionMath.Of(x.Amount, x.CommissionRateApplied!.Value))
                                       - oRefs.Where(x => x.CommissionRateApplied != null).Sum(x => CommissionMath.Of(x.Amount, x.CommissionRateApplied!.Value));
                }

                return new MerchantCommissionDto
                {
                    MerchantId = merchant.Id,
                    MerchantName = merchant.BusinessName,
                    PaidOrderCount = mPayments.Select(x => x.OrderId).Distinct().Count(),
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = net,
                    CommissionRate = merchant.CommissionRate,
                    CommissionAmount = totalCommission,
                    PayoutAmount = net - totalCommission,
                    OrdersWithoutCommissionSnapshot = uncovered,
                    UncoveredNetAmount = uncoveredNet
                };
            }).OrderByDescending(x => x.NetRevenue).ToList();
        }

        private static DateTime Hour(DateTime local) => new(local.Year, local.Month, local.Day, local.Hour, 0, 0);

        public async Task<BaseResponse<CommissionPageDto>> GetCommissionsPageAsync(FinancePeriodQuery query)
        {
            var all = await GetCommissionsAsync(query);
            if (!all.Succeeded || all.Data == null)
                return new BaseResponse<CommissionPageDto> { Succeeded = false, StatusCode = all.StatusCode, Message = all.Message, Meta = all.Meta };
            var page = Math.Max(1, query.Page ?? 1);
            var take = Math.Clamp(query.Take ?? 20, 1, 100);
            var rows = all.Data.OrderByDescending(x => x.NetRevenue).ThenBy(x => x.MerchantId).ToList();
            return Success(new CommissionPageDto
            {
                Page = page, Take = take, Total = rows.Count, GeneratedAt = DateTime.UtcNow,
                Items = rows.Skip((page - 1) * take).Take(take).ToList(),
                GrossRevenue = rows.Sum(x => x.GrossRevenue), RefundedAmount = rows.Sum(x => x.RefundedAmount), NetRevenue = rows.Sum(x => x.NetRevenue),
                CommissionAmount = rows.Sum(x => x.CommissionAmount), PayoutAmount = rows.Sum(x => x.PayoutAmount),
                OrdersWithoutCommissionSnapshot = rows.Sum(x => x.OrdersWithoutCommissionSnapshot),
                CommissionComplete = rows.All(x => x.CommissionComplete)
            });
        }

        public async Task<BaseResponse<List<MerchantCommissionDto>>> GetCommissionsAsync(FinancePeriodQuery query)
        {
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var (from, to, error) = Period(query.FromDate, query.ToDate);
            if (error != null)
                return BadRequest<List<MerchantCommissionDto>>(error);
            var rows = await BuildCommissionsAsync(from, to, query.MerchantId);
            await snapshot.CompleteAsync();
            return Success(rows, rows.Count == 0 ? "Không có dữ liệu trong khoảng thời gian này." : null);
        }

        public async Task<BaseResponse<SettlementQuoteDto>> QuoteSettlementAsync(CreateSettlementCommand command)
        {
            if (command.FromDate <= 0 || command.ToDate <= 0) return BadRequest<SettlementQuoteDto>("Vui lòng chọn kỳ đối soát.");
            var (from, to, error) = Period(command.FromDate, command.ToDate);
            if (error != null || to > DateTime.UtcNow) return BadRequest<SettlementQuoteDto>(error ?? "Kỳ đối soát phải đã kết thúc.");
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var merchant = await _merchantRepo.FindAsync(command.MerchantId);
            if (merchant == null) return NotFound<SettlementQuoteDto>("Không tìm thấy Merchant.");
            var quote = await BuildQuoteAsync(merchant, from, to, null);
            if (await _settlementRepo.AnyAsync(x => !x.IsDeleted && x.MerchantId == merchant.Id && x.PeriodFrom <= to && x.PeriodTo >= from))
            { quote.CanCreate = false; quote.BlockingCode = "SettlementOverlap"; }
            if (await _settlementRepo.AnyAsync(x => !x.IsDeleted && x.MerchantId == merchant.Id && x.PeriodTo >= from) && quote.BlockingCode == null)
            { quote.CanCreate = false; quote.BlockingCode = "SettlementChronology"; }
            await snapshot.CompleteAsync(); return Success(quote);
        }

        private async Task<SettlementQuoteDto> BuildQuoteAsync(Merchant merchant, DateTime from, DateTime to, MerchantCommissionDto? summary)
        {
            summary ??= new MerchantCommissionDto { MerchantId = merchant.Id, MerchantName = merchant.BusinessName, CommissionRate = merchant.CommissionRate };
            var carrySources = await _settlementRepo.GetListAsync(x => !x.IsDeleted && x.MerchantId == merchant.Id && x.Status == SettlementStatus.CarryForward && x.CarriedToSettlementId == null && x.PeriodTo < from);
            var breakdown = await BuildBreakdownAsync(new MerchantSettlement
            {
                MerchantId = merchant.Id, PeriodFrom = from, PeriodTo = to,
                CommissionAmount = summary.CommissionAmount, PayoutAmount = summary.PayoutAmount
            }, true);
            // Derive the payable totals from the exact source IDs frozen in the breakdown, never a second live query.
            summary.GrossRevenue = breakdown.Lines.Sum(x => x.Collected);
            summary.RefundedAmount = breakdown.Lines.Sum(x => x.Refunded);
            summary.NetRevenue = summary.GrossRevenue - summary.RefundedAmount;
            summary.PaidOrderCount = breakdown.Lines.Count(x => x.PaymentTransactionIds.Count > 0);
            summary.OrdersWithoutCommissionSnapshot = breakdown.Lines.Count(x => x.CommissionRate == null);
            summary.UncoveredNetAmount = breakdown.Lines.Where(x => x.CommissionRate == null).Sum(x => x.Net);
            summary.CommissionAmount = breakdown.Lines.Sum(x => x.Commission ?? 0);
            summary.PeriodPayoutAmount = summary.NetRevenue - summary.CommissionAmount;
            summary.CarryForwardAmount = carrySources.Sum(x => x.PayoutAmount);
            summary.PayoutAmount = summary.PeriodPayoutAmount + summary.CarryForwardAmount;
            breakdown.PeriodPayoutAmount = summary.PeriodPayoutAmount; breakdown.CarryForwardAmount = summary.CarryForwardAmount;
            breakdown.CarryForwardSources = carrySources.OrderBy(x => x.PeriodTo).ThenBy(x => x.Id).Select(x => new SettlementCarryForwardDto { SourceSettlementId = x.Id, PeriodFrom = x.PeriodFrom, PeriodTo = x.PeriodTo, Amount = x.PayoutAmount }).ToList();
            breakdown.SettledCommissionAmount = summary.CommissionAmount;
            breakdown.SettledPayoutAmount = summary.PayoutAmount;
            return new SettlementQuoteDto
            {
                Summary = summary, Breakdown = breakdown, GeneratedAt = DateTime.UtcNow,
                QuoteToken = AdministrativeWriteGuard.Version(new { merchant.Id, from, to, summary, breakdown.Lines, CarrySources = carrySources.Select(x => x.Id).OrderBy(x => x).ToArray() }),
                CanCreate = summary.CommissionComplete,
                BlockingCode = !summary.CommissionComplete ? "CommissionSnapshotMissing" : null
            };
        }

        public async Task<BaseResponse<AdministrativeOperationDto>> GetOperationAsync(Guid id)
        {
            var operation = await Writes.FindAsync(id, CurrentUser.Id!.Value, "Finance");
            return operation == null ? NotFound<AdministrativeOperationDto>("Chưa có kết quả đã commit; không chứng minh yêu cầu đang chạy đã thất bại.") :
                Success(new AdministrativeOperationDto { OperationId = operation.Id, Action = operation.Action, ResourceId = operation.ResourceId, CompletedAt = operation.CompletedAt, Result = JsonSerializer.Deserialize<JsonElement>(operation.ResponseJson) });
        }

        public async Task<BaseResponse<SettlementDto>> CreateSettlementAsync(CreateSettlementCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<SettlementDto>(command.OperationId, CurrentUser.Id!.Value, "Finance", "CreateSettlement", command.MerchantId.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync($"finance-merchant:{command.MerchantId}");
            if (command.FromDate <= 0 || command.ToDate <= 0)
                return BadRequest<SettlementDto>("Vui lòng chọn kỳ đối soát.");
            var (from, to, error) = Period(command.FromDate, command.ToDate);
            if (error != null)
                return BadRequest<SettlementDto>(error);
            if (to > DateTime.UtcNow)
                return BadRequest<SettlementDto>("Chỉ tạo kỳ đối soát cho khoảng thời gian đã kết thúc.");
            var merchant = await _merchantRepo.FindAsync(command.MerchantId);
            if (merchant == null)
                return NotFound<SettlementDto>("Không tìm thấy Merchant!");

            // A period may be settled only once per Merchant; overlapping periods would pay twice.
            if (await _settlementRepo.AnyAsync(x => x.MerchantId == merchant.Id && !x.IsDeleted &&
                                                     x.PeriodFrom <= to && x.PeriodTo >= from))
                return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "SettlementOverlap", "Kỳ đối soát bị trùng với kỳ đã tạo cho Merchant này.");

            if (await _settlementRepo.AnyAsync(x => !x.IsDeleted && x.MerchantId == merchant.Id && x.PeriodTo >= from))
                return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "SettlementChronology", "Cần tạo kỳ đối soát theo thứ tự thời gian sau các kỳ đã chốt.");
            var preview = await BuildQuoteAsync(merchant, from, to, null);
            if (!preview.CanCreate) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.BadRequest, preview.BlockingCode!, "Kỳ thiếu dữ liệu tỷ lệ hoa hồng; cần đối chiếu trước khi chốt.");
            if (string.IsNullOrWhiteSpace(command.QuoteToken)) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.BadRequest, "QuoteRequired", "Cần lấy quoteToken từ POST /finance/settlements/quote.");
            if (command.QuoteToken != preview.QuoteToken) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "QuoteChanged", "Số liệu đối soát đã thay đổi; hãy xem lại quote.");
            var settlement = new MerchantSettlement
            {
                MerchantId = merchant.Id,
                MerchantName = merchant.BusinessName,
                PeriodFrom = from,
                PeriodTo = to,
                OrderCount = preview.Summary.PaidOrderCount,
                GrossRevenue = preview.Summary.GrossRevenue,
                RefundedAmount = preview.Summary.RefundedAmount,
                NetRevenue = preview.Summary.NetRevenue,
                CommissionRate = merchant.CommissionRate,
                CommissionAmount = preview.Summary.CommissionAmount,
                PayoutAmount = preview.Summary.PayoutAmount,
                PeriodPayoutAmount = preview.Summary.PeriodPayoutAmount, CarryForwardAmount = preview.Summary.CarryForwardAmount,
                Status = preview.Summary.PayoutAmount < 0 ? SettlementStatus.CarryForward : preview.Summary.PayoutAmount == 0 ? SettlementStatus.NoPayout : SettlementStatus.Pending,
                CreatedByUserId = CurrentUser.Id
            };
            settlement.BreakdownSnapshotJson = JsonSerializer.Serialize(preview.Breakdown);
            await _settlementRepo.InsertAsync(settlement, autoSave: true);
            var paymentIds = preview.Breakdown.Lines.SelectMany(x => x.PaymentTransactionIds).Distinct().ToList();
            var refundIds = preview.Breakdown.Lines.SelectMany(x => x.RefundRequestIds).Distinct().ToList();
            if (!await LazyServiceProvider.LazyGetRequiredService<ISettlementSourceWriter>().AttachAsync(settlement.Id, paymentIds, refundIds))
            {
                await uow.RollbackAsync();
                return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "SettlementSourcesChanged", "Giao dịch nguồn đã thuộc kỳ đối soát khác; vui lòng xem lại báo giá đối soát.");
            }
            var carrySources = await _settlementRepo.GetListAsync(x => !x.IsDeleted && x.MerchantId == merchant.Id && x.Status == SettlementStatus.CarryForward && x.CarriedToSettlementId == null && x.PeriodTo < from);
            foreach (var source in carrySources) { source.CarriedToSettlementId = settlement.Id; await _settlementRepo.UpdateAsync(source); }
            await _notifications.SendAsync([merchant.Id], NotificationType.SettlementCreated, "Kỳ đối soát mới",
                $"Kỳ đối soát {(from + VietnamOffset):dd/MM/yyyy} - {(to + VietnamOffset):dd/MM/yyyy}: doanh thu {settlement.NetRevenue:N0}đ, " +
                $"hoa hồng {settlement.CommissionAmount:N0}đ, thực nhận {settlement.PayoutAmount:N0}đ.");
            await Audit.LogAsync(new AuditEntry("Finance", "CreateSettlement", "Settlement", settlement.Id.ToString(), merchant.BusinessName)
            {
                Details = new { merchantId = merchant.Id, settlement.PeriodFrom, settlement.PeriodTo, settlement.OrderCount, settlement.NetRevenue,
                                settlement.CommissionAmount, settlement.PayoutAmount }
            });
            var response = Created(ToDto(settlement), "Đã tạo kỳ đối soát.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "CreateSettlement", command.MerchantId.ToString(), command, response);
            await uow.CompleteAsync();
            return response;
        }

        private static readonly Dictionary<string, string> SettlementSortFields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Id", ["PeriodFrom"] = "PeriodFrom", ["PeriodTo"] = "PeriodTo", ["PaidAt"] = "PaidAt",
            ["CreatedOn"] = "CreatedOn", ["MerchantName"] = "MerchantName", ["PayoutAmount"] = "PayoutAmount", ["Status"] = "Status"
        };
        public async Task<PaginatedResult<SettlementDto>> GetSettlementsAsync(SettlementFilterQuery query)
        {
            if (!CurrentUser.Id.HasValue || !CurrentUser.IsInRole("Admin") && !CurrentUser.IsInRole("Merchant"))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden, Message = "Không có quyền xem đối soát." };
            if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "Trạng thái đối soát không hợp lệ." };
            var paidDates = new BaseFilterQuery { FromDate = query.PaidFromDate, ToDate = query.PaidToDate };
            if (query.DateRangeError() != null || paidDates.DateRangeError() != null || query.Keyword?.Length > 200 || !WhitelistedQuerySort.IsValid(query.SortBy, SettlementSortFields))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "Khoảng ngày hoặc tiêu chí sắp xếp đối soát không hợp lệ (tối đa 8 tiêu chí)." };
            var queryable = (await _settlementRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            if (query.FromDate > 0) { var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime; queryable = queryable.Where(x => x.PeriodTo >= from); }
            if (query.ToDate > 0) { var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime; queryable = queryable.Where(x => x.PeriodFrom <= to); }
            if (query.PaidFromDate > 0) { var from = DateTimeOffset.FromUnixTimeSeconds(query.PaidFromDate).UtcDateTime; queryable = queryable.Where(x => x.PaidAt >= from); }
            if (query.PaidToDate > 0) { var to = DateTimeOffset.FromUnixTimeSeconds(query.PaidToDate).UtcDateTime; queryable = queryable.Where(x => x.PaidAt <= to); }
            if (!string.IsNullOrWhiteSpace(query.Keyword)) { var term = query.Keyword.Trim().ToLower(); queryable = queryable.Where(x => x.MerchantName.ToLower().Contains(term) || x.PaymentReference != null && x.PaymentReference.ToLower().Contains(term)); }
            // Merchants only see their own settlements.
            var merchantId = CurrentUser.IsInRole("Admin") ? query.MerchantId : CurrentUser.Id;
            if (merchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == merchantId.Value);
            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(WhitelistedQuerySort.Apply(queryable, query.SortBy, SettlementSortFields, new SortOption { Field = "PeriodTo", IsDescending = true })
                .Skip((int)Math.Min(int.MaxValue, ((long)page - 1) * take)).Take(take));
            return Success(items.Select(ToDto).ToList(), total, page, take, total == 0 ? "Chưa có kỳ đối soát nào." : null);
        }

        public async Task<BaseResponse<SettlementDto>> GetSettlementAsync(Guid id)
        {
            var settlement = await _settlementRepo.FindAsync(id);
            return settlement == null || settlement.IsDeleted || (!CurrentUser.IsInRole("Admin") && settlement.MerchantId != CurrentUser.Id)
                ? NotFound<SettlementDto>("Không tìm thấy kỳ đối soát.") : Success(ToDto(settlement));
        }

        public async Task<SettlementLinesPageDto> GetSettlementLinesAsync(Guid id, int page, int take)
        {
            static SettlementLinesPageDto Page(PaginatedResult<SettlementOrderLineDto> r, bool snapshot) => new()
            {
                List = r.List, StatusCode = r.StatusCode, CurrentPage = r.CurrentPage, TotalPages = r.TotalPages, Total = r.Total,
                PageSize = r.PageSize, Message = r.Message, Succeeded = r.Succeeded, IsSnapshot = snapshot, GeneratedAt = DateTime.UtcNow,
                Basis = snapshot ? "SettlementSnapshot: dòng chụp lúc chốt kỳ, bất biến."
                                 : "LegacyRecomputed: kỳ cũ chưa có snapshot, dòng tính lại từ giao dịch hiện tại để tham khảo; tổng tiền của kỳ giữ nguyên."
            };
            page = Math.Max(1, page); take = Math.Clamp(take, 1, 100);
            if (!CurrentUser.Id.HasValue) return new() { Succeeded = false, StatusCode = HttpStatusCode.Forbidden };
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var stored = await LazyServiceProvider.LazyGetRequiredService<ISettlementSnapshotReader>().ReadAsync(id, CurrentUser.IsInRole("Admin") ? null : CurrentUser.Id, page, take);
            if (stored.HasValue)
            {
                var lines = stored.Value.Lines.Select(x => JsonSerializer.Deserialize<SettlementOrderLineDto>(x)!).ToList();
                await snapshot.CompleteAsync(); return Page(Success(lines, stored.Value.Total, page, take), true);
            }
            // Legacy periods have no frozen rows: the bounded live breakdown is explicitly a fallback.
            var result = await GetSettlementBreakdownAsync(id);
            if (!result.Succeeded || result.Data == null) return new() { Succeeded = false, StatusCode = result.StatusCode, Message = result.Message };
            page = Math.Max(1, page); take = Math.Clamp(take, 1, 100);
            await snapshot.CompleteAsync();
            return Page(Success(result.Data.Lines.Skip((int)Math.Min(int.MaxValue, ((long)page - 1) * take)).Take(take).ToList(), result.Data.Lines.Count, page, take), result.Data.IsSnapshot);
        }

        public async Task<BaseResponse<SettlementBreakdownDto>> GetSettlementBreakdownAsync(Guid id)
        {
            var settlement = await _settlementRepo.FindAsync(id);
            if (settlement == null || settlement.IsDeleted ||
                !CurrentUser.IsInRole("Admin") && settlement.MerchantId != CurrentUser.Id)
                return NotFound<SettlementBreakdownDto>("Không tìm thấy kỳ đối soát!");
            if (settlement.BreakdownSnapshotJson != null)
            {
                var snapshot = JsonSerializer.Deserialize<SettlementBreakdownDto>(settlement.BreakdownSnapshotJson)!;
                snapshot.SettlementId = settlement.Id; snapshot.Status = settlement.Status; snapshot.IsSnapshot = true; snapshot.CarriedToSettlementId = settlement.CarriedToSettlementId;
                return Success(snapshot);
            }
            // Legacy periods expose current source lines explicitly as non-snapshot; settled totals are unchanged.
            return Success(await BuildBreakdownAsync(settlement));
        }

        private async Task<SettlementBreakdownDto> BuildBreakdownAsync(MerchantSettlement settlement, bool unsettledOnly = false)
        {
            var payments = (await PaymentsAsync(settlement.PeriodFrom, settlement.PeriodTo, settlement.MerchantId, unsettledOnly))
                .Where(x => x.Purpose == PaymentPurpose.Order).ToList();
            var refunds = await RefundsAsync(settlement.PeriodFrom, settlement.PeriodTo, settlement.MerchantId, unsettledOnly);
            var orderIds = payments.Select(x => x.OrderId).Concat(refunds.Select(x => x.OrderId)).Distinct().ToList();
            var codes = (await _orderRepo.GetListAsync(o => orderIds.Contains(o.Id))).ToDictionary(o => o.Id, o => o.OrderCode);
            var lines = orderIds.Select(oid =>
            {
                var collected = payments.Where(x => x.OrderId == oid).Sum(x => x.Amount);
                var refunded = refunds.Where(x => x.OrderId == oid).Sum(x => x.Amount);
                var oPays = payments.Where(x => x.OrderId == oid).ToList();
                var oRefs = refunds.Where(x => x.OrderId == oid).ToList();
                var rate = oPays.FirstOrDefault()?.CommissionRateApplied ?? oRefs.FirstOrDefault()?.CommissionRateApplied;
                var complete = oPays.All(x => x.CommissionRateApplied != null) && oRefs.All(x => x.CommissionRateApplied != null);
                decimal? commission = complete
                    ? oPays.Sum(x => CommissionMath.Of(x.Amount, x.CommissionRateApplied!.Value)) - oRefs.Sum(x => CommissionMath.Of(x.Amount, x.CommissionRateApplied!.Value))
                    : null;
                return new SettlementOrderLineDto
                {
                    OrderId = oid, OrderCode = codes.GetValueOrDefault(oid, string.Empty), Collected = collected, Refunded = refunded,
                    PaymentTransactionIds = payments.Where(x => x.OrderId == oid).Select(x => x.Id).OrderBy(x => x).ToList(),
                    RefundRequestIds = refunds.Where(x => x.OrderId == oid).Select(x => x.Id).OrderBy(x => x).ToList(),
                    CommissionRate = rate, Commission = commission, Payout = commission.HasValue ? collected - refunded - commission.Value : null
                };
            }).OrderBy(x => x.OrderCode).ToList();
            return new SettlementBreakdownDto
            {
                SettlementId = settlement.Id, PeriodFrom = settlement.PeriodFrom, PeriodTo = settlement.PeriodTo, Status = settlement.Status,
                SettledCommissionAmount = settlement.CommissionAmount, SettledPayoutAmount = settlement.PayoutAmount,
                OrdersWithoutCommissionSnapshot = lines.Count(x => !x.CommissionRate.HasValue), Lines = lines
            };
        }

        public async Task<BaseResponse<SettlementDto>> ConfirmSettlementPaidAsync(Guid id, ConfirmSettlementPaidCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var payload = new { id, command.ExpectedVersion, Reference = command.Reference?.Trim() };
            if (await Writes.ReplayAsync<SettlementDto>(command.OperationId, CurrentUser.Id!.Value, "Finance", "ConfirmSettlementPaid", id.ToString(), payload) is { } replay) return replay;
            await Writes.LockAsync($"finance-settlement:{id}");
            var reference = command.Reference?.Trim();
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
                return BadRequest<SettlementDto>("Vui lòng nhập mã tham chiếu chuyển khoản (tối đa 120 ký tự).");
            var settlement = await _settlementRepo.FindAsync(id);
            if (settlement == null || settlement.IsDeleted)
                return NotFound<SettlementDto>("Không tìm thấy kỳ đối soát!");
            if (settlement.Status == SettlementStatus.Paid)
            {
                if (settlement.PaymentReference != reference) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "PaymentReferenceChanged", "Kỳ đã chi trả với mã tham chiếu khác.");
                var prior = Success(ToDto(settlement), "Kỳ đối soát đã được ghi nhận chi trả trước đó.");
                await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "ConfirmSettlementPaid", id.ToString(), payload, prior);
                await uow.CompleteAsync(); return prior;
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != ToDto(settlement).Version) return AdministrativeWriteGuard.Error<SettlementDto>(HttpStatusCode.Conflict, "VersionMismatch", "Kỳ đối soát đã thay đổi.");
            if (settlement.PayoutAmount <= 0) return BadRequest<SettlementDto>("Không ghi chi trả cho kỳ có thực nhận bằng 0 hoặc âm.");

            settlement.Status = SettlementStatus.Paid;
            settlement.PaymentReference = reference;
            settlement.PaidAt = DateTime.UtcNow;
            await _settlementRepo.UpdateAsync(settlement, autoSave: true);
            await _notifications.SendAsync([settlement.MerchantId], NotificationType.SettlementPaid, "Đã chi trả đối soát",
                $"WashGo đã chuyển {settlement.PayoutAmount:N0}đ cho kỳ đối soát {(settlement.PeriodFrom + VietnamOffset):dd/MM/yyyy} - " +
                $"{(settlement.PeriodTo + VietnamOffset):dd/MM/yyyy} (mã {reference}).");
            await Audit.LogAsync(new AuditEntry("Finance", "ConfirmSettlementPaid", "Settlement", settlement.Id.ToString(), reference)
            {
                ChangedFields = ["status", "paymentReference", "paidAt"],
                Details = new { merchantId = settlement.MerchantId, settlement.PayoutAmount }
            });
            var response = Success(ToDto(settlement), "Đã ghi nhận chi trả cho Merchant.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Finance", "ConfirmSettlementPaid", id.ToString(), payload, response);
            await uow.CompleteAsync(); return response;
        }

        private static SettlementDto ToDto(MerchantSettlement x) => new()
        {
            Version = AdministrativeWriteGuard.Version(new { x.Id, x.Status, x.PaymentReference, PaidAt = x.PaidAt?.Ticks / 10, x.PayoutAmount, x.PeriodFrom, x.PeriodTo, x.CommissionAmount, x.RefundedAmount, x.CarriedToSettlementId, x.CarryForwardAmount }),
            Id = x.Id,
            MerchantId = x.MerchantId,
            MerchantName = x.MerchantName,
            PeriodFrom = x.PeriodFrom,
            PeriodTo = x.PeriodTo,
            OrderCount = x.OrderCount,
            GrossRevenue = x.GrossRevenue,
            RefundedAmount = x.RefundedAmount,
            NetRevenue = x.NetRevenue,
            CommissionRate = x.CommissionRate,
            CommissionAmount = x.CommissionAmount,
            PayoutAmount = x.PayoutAmount, PeriodPayoutAmount = x.PeriodPayoutAmount, CarryForwardAmount = x.CarryForwardAmount, CarriedToSettlementId = x.CarriedToSettlementId,
            Status = x.Status,
            PaymentReference = x.PaymentReference,
            PaidAt = x.PaidAt,
            CreatedOn = x.CreatedOn
        };
    }
}
