using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private sealed record PaymentRow(Guid OrderId, Guid? MerchantId, PaymentPurpose Purpose, decimal Amount, DateTime CompletedAt, decimal CommissionRateApplied);
        private sealed record RefundRow(Guid OrderId, Guid? MerchantId, decimal Amount, DateTime RefundedAt, decimal CommissionRateApplied);

        private static (DateTime From, DateTime To, string? Error) Period(long? fromDate, long? toDate)
        {
            var to = toDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(toDate.Value).UtcDateTime : DateTime.UtcNow;
            var from = fromDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromDate.Value).UtcDateTime : to.AddDays(-30);
            if (from > to)
                return (from, to, "Thời gian bắt đầu phải trước thời gian kết thúc.");
            if ((to - from).TotalDays > MaxPeriodDays)
                return (from, to, $"Khoảng thời gian tối đa {MaxPeriodDays} ngày.");
            return (from, to, null);
        }

        private async Task<List<PaymentRow>> PaymentsAsync(DateTime periodFrom, DateTime periodTo, Guid? merchantId)
        {
            var payments = await _paymentRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var query = from p in payments
                        join o in orders on p.OrderId equals o.Id
                        where (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded)
                              && p.CompletedAt != null && p.CompletedAt >= periodFrom && p.CompletedAt <= periodTo
                              && (merchantId == null || o.MerchantId == merchantId)
                        select new PaymentRow(o.Id, o.MerchantId, p.Purpose, p.Amount, p.CompletedAt!.Value, o.CommissionRateApplied);
            return await AsyncExecuter.ToListAsync(query);
        }

        private async Task<List<RefundRow>> RefundsAsync(DateTime periodFrom, DateTime periodTo, Guid? merchantId)
        {
            var refunds = await _refundRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var query = from r in refunds
                        join o in orders on r.OrderId equals o.Id
                        where r.Status == RefundRequestStatus.Refunded && r.RefundedAt != null
                              && r.RefundedAt >= periodFrom && r.RefundedAt <= periodTo
                              && (merchantId == null || o.MerchantId == merchantId)
                        select new RefundRow(o.Id, o.MerchantId, r.Amount, r.RefundedAt!.Value, o.CommissionRateApplied);
            return await AsyncExecuter.ToListAsync(query);
        }

        private async Task<Dictionary<Guid, Merchant>> MerchantsAsync(IEnumerable<Guid?> ids)
        {
            var list = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            return (await _merchantRepo.GetListAsync(x => list.Contains(x.Id))).ToDictionary(x => x.Id);
        }

        public async Task<BaseResponse<RevenueReportDto>> GetRevenueReportAsync(FinancePeriodQuery query)
        {
            var (from, to, error) = Period(query.FromDate, query.ToDate);
            if (error != null)
                return BadRequest<RevenueReportDto>(error);
            var payments = await PaymentsAsync(from, to, query.MerchantId);
            var refunds = await RefundsAsync(from, to, query.MerchantId);
            var merchants = await MerchantsAsync(payments.Select(x => x.MerchantId).Concat(refunds.Select(x => x.MerchantId)));

            var laundryOrderIds = payments.Where(x => x.Purpose == PaymentPurpose.Order).Select(x => x.OrderId).Distinct().ToList();
            var items = await _itemRepo.GetListAsync(x => laundryOrderIds.Contains(x.WashOrderId));

            var report = new RevenueReportDto
            {
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
            var empty = payments.Count == 0 && refunds.Count == 0;
            return Success(report, empty ? "Không có dữ liệu trong khoảng thời gian này." : null);
        }

        public async Task<BaseResponse<string>> ExportRevenueReportCsvAsync(FinancePeriodQuery query)
        {
            var result = await GetRevenueReportAsync(query);
            if (!result.Succeeded || result.Data == null)
                return BadRequest<string>(result.Message ?? "Không thể xuất báo cáo. Vui lòng thử lại.");
            var r = result.Data;
            static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
            static string Q(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
            var csv = new StringBuilder();
            csv.AppendLine("Muc,Gia tri");
            csv.AppendLine($"Tu ngay,{r.FromDate:yyyy-MM-dd HH:mm}");
            csv.AppendLine($"Den ngay,{r.ToDate:yyyy-MM-dd HH:mm}");
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

        private async Task<List<MerchantCommissionDto>> BuildCommissionsAsync(DateTime from, DateTime to, Guid? merchantId)
        {
            var payments = (await PaymentsAsync(from, to, merchantId)).Where(x => x.Purpose == PaymentPurpose.Order && x.MerchantId != null).ToList();
            var refunds = (await RefundsAsync(from, to, merchantId)).Where(x => x.MerchantId != null).ToList();
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
                // Refunds of laundry money reduce the base; refunds cannot exceed what was collected.
                var refunded = Math.Min(gross, mRefunds.Sum(x => x.Amount));
                var net = gross - refunded;

                // Tính hoa hồng theo từng đơn (UC 2.3.2 & UC 3.8):
                // Mỗi đơn giặt sử dụng CommissionRateApplied được snapshot trên đơn (hoặc fallback về merchant.CommissionRate nếu đơn cũ = 0)
                var orderIds = mPayments.Select(x => x.OrderId).Concat(mRefunds.Select(x => x.OrderId)).Distinct().ToList();
                decimal totalCommission = 0m;
                foreach (var oId in orderIds)
                {
                    var oGross = mPayments.Where(x => x.OrderId == oId).Sum(x => x.Amount);
                    var oRefund = Math.Min(oGross, mRefunds.Where(x => x.OrderId == oId).Sum(x => x.Amount));
                    var oNet = oGross - oRefund;
                    if (oNet <= 0) continue;

                    var rate = mPayments.FirstOrDefault(x => x.OrderId == oId)?.CommissionRateApplied
                               ?? mRefunds.FirstOrDefault(x => x.OrderId == oId)?.CommissionRateApplied
                               ?? 0m;
                    if (rate <= 0m)
                    {
                        rate = merchant.CommissionRate;
                    }
                    totalCommission += decimal.Round(oNet * rate / 100m, 0, MidpointRounding.AwayFromZero);
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
                    PayoutAmount = net - totalCommission
                };
            }).OrderByDescending(x => x.NetRevenue).ToList();
        }

        public async Task<BaseResponse<List<MerchantCommissionDto>>> GetCommissionsAsync(FinancePeriodQuery query)
        {
            var (from, to, error) = Period(query.FromDate, query.ToDate);
            if (error != null)
                return BadRequest<List<MerchantCommissionDto>>(error);
            var rows = await BuildCommissionsAsync(from, to, query.MerchantId);
            return Success(rows, rows.Count == 0 ? "Không có dữ liệu trong khoảng thời gian này." : null);
        }

        public async Task<BaseResponse<SettlementDto>> CreateSettlementAsync(CreateSettlementCommand command)
        {
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

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            // A period may be settled only once per Merchant; overlapping periods would pay twice.
            if (await _settlementRepo.AnyAsync(x => x.MerchantId == merchant.Id && !x.IsDeleted &&
                                                     x.PeriodFrom < to && x.PeriodTo > from))
                return BadRequest<SettlementDto>("Kỳ đối soát bị trùng với kỳ đã tạo cho Merchant này.");

            var row = (await BuildCommissionsAsync(from, to, merchant.Id)).FirstOrDefault();
            var settlement = new MerchantSettlement
            {
                MerchantId = merchant.Id,
                MerchantName = merchant.BusinessName,
                PeriodFrom = from,
                PeriodTo = to,
                OrderCount = row?.PaidOrderCount ?? 0,
                GrossRevenue = row?.GrossRevenue ?? 0,
                RefundedAmount = row?.RefundedAmount ?? 0,
                NetRevenue = row?.NetRevenue ?? 0,
                CommissionRate = merchant.CommissionRate,
                CommissionAmount = row?.CommissionAmount ?? 0,
                PayoutAmount = row?.PayoutAmount ?? 0,
                Status = SettlementStatus.Pending,
                CreatedByUserId = CurrentUser.Id
            };
            await _settlementRepo.InsertAsync(settlement, autoSave: true);
            await _notifications.SendAsync([merchant.Id], NotificationType.SettlementCreated, "Kỳ đối soát mới",
                $"Kỳ đối soát {(from + VietnamOffset):dd/MM/yyyy} - {(to + VietnamOffset):dd/MM/yyyy}: doanh thu {settlement.NetRevenue:N0}đ, " +
                $"hoa hồng {settlement.CommissionAmount:N0}đ, thực nhận {settlement.PayoutAmount:N0}đ.");
            await uow.CompleteAsync();
            return Created(ToDto(settlement), "Đã tạo kỳ đối soát.");
        }

        public async Task<PaginatedResult<SettlementDto>> GetSettlementsAsync(SettlementFilterQuery query)
        {
            var queryable = (await _settlementRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            // Merchants only see their own settlements.
            var merchantId = CurrentUser.IsInRole("Admin") ? query.MerchantId : CurrentUser.Id;
            if (merchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == merchantId.Value);
            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.PeriodTo)
                .Skip((page - 1) * take).Take(take));
            return Success(items.Select(ToDto).ToList(), total, page, take, total == 0 ? "Chưa có kỳ đối soát nào." : null);
        }

        public async Task<BaseResponse<SettlementDto>> ConfirmSettlementPaidAsync(Guid id, ConfirmSettlementPaidCommand command)
        {
            var reference = command.Reference?.Trim();
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
                return BadRequest<SettlementDto>("Vui lòng nhập mã tham chiếu chuyển khoản (tối đa 120 ký tự).");
            var settlement = await _settlementRepo.FindAsync(id);
            if (settlement == null || settlement.IsDeleted)
                return NotFound<SettlementDto>("Không tìm thấy kỳ đối soát!");
            if (settlement.Status == SettlementStatus.Paid)
                return Success(ToDto(settlement), "Kỳ đối soát đã được ghi nhận chi trả trước đó.");

            settlement.Status = SettlementStatus.Paid;
            settlement.PaymentReference = reference;
            settlement.PaidAt = DateTime.UtcNow;
            await _settlementRepo.UpdateAsync(settlement, autoSave: true);
            await _notifications.SendAsync([settlement.MerchantId], NotificationType.SettlementPaid, "Đã chi trả đối soát",
                $"WashGo đã chuyển {settlement.PayoutAmount:N0}đ cho kỳ đối soát {(settlement.PeriodFrom + VietnamOffset):dd/MM/yyyy} - " +
                $"{(settlement.PeriodTo + VietnamOffset):dd/MM/yyyy} (mã {reference}).");
            return Success(ToDto(settlement), "Đã ghi nhận chi trả cho Merchant.");
        }

        private static SettlementDto ToDto(MerchantSettlement x) => new()
        {
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
            PayoutAmount = x.PayoutAmount,
            Status = x.Status,
            PaymentReference = x.PaymentReference,
            PaidAt = x.PaidAt,
            CreatedOn = x.CreatedOn
        };
    }
}
