using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FinanceContracts.Commands;
using WashGo.Application.Contracts.Features.FinanceContracts.Queries;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Application.Contracts.Features.FinanceContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using System.Collections.Generic;
using System.Data;

namespace WashGo.Application.Features.FinanceApplication.Services
{
    /// <summary>
    /// Historical commission rates are never guessed: orders without a rate (or with an unverified legacy 0%) are listed for review,
    /// and an Admin confirms or corrects each one with written evidence. Every change is audited; settled periods are never rewritten.
    /// </summary>
    public class CommissionReviewAppService : WashGoAppService, ICommissionReviewAppService
    {
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<PaymentTransaction, Guid> _paymentRepo;
        private readonly IRepository<RefundRequest, Guid> _refundRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;

        public CommissionReviewAppService(IWashOrderRepository orderRepo, IRepository<PaymentTransaction, Guid> paymentRepo,
            IRepository<RefundRequest, Guid> refundRepo, IRepository<Merchant, Guid> merchantRepo, IRepository<WashOrderTimeline, Guid> timelineRepo)
        {
            _orderRepo = orderRepo;
            _paymentRepo = paymentRepo;
            _refundRepo = refundRepo;
            _merchantRepo = merchantRepo;
            _timelineRepo = timelineRepo;
        }

        private async Task<IQueryable<WashOrder>> CandidatesAsync(Guid? merchantId)
        {
            var payments = await _paymentRepo.GetQueryableAsync();
            var q = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted && o.MerchantId != null &&
                (o.CommissionSnapshotSource == "LegacyUnverified" ||
                 o.CommissionRateApplied == null && payments.Any(p => p.OrderId == o.Id && p.Purpose == PaymentPurpose.Order && p.CompletedAt != null &&
                     (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded))));
            if (merchantId.HasValue) q = q.Where(o => o.MerchantId == merchantId);
            return q;
        }

        public async Task<BaseResponse<CommissionReviewPageDto>> GetReviewAsync(CommissionReviewQuery query)
        {
            var page = Math.Max(1, query.Page ?? 1);
            var take = Math.Clamp(query.Take ?? 20, 1, 100);
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
            var q = await CandidatesAsync(query.MerchantId);
            var total = await AsyncExecuter.CountAsync(q);
            var orders = await AsyncExecuter.ToListAsync(q.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Skip((page - 1) * take).Take(take));
            var items = await ToItemsAsync(orders);
            await snapshot.CompleteAsync();
            return Success(new CommissionReviewPageDto { Page = page, Take = take, Total = total, Items = items, GeneratedAt = DateTime.UtcNow },
                total == 0 ? "Không có đơn nào cần đối chiếu tỷ lệ hoa hồng." : null);
        }

        private async Task<List<CommissionReviewItemDto>> ToItemsAsync(List<WashOrder> orders)
        {
            var ids = orders.Select(o => o.Id).ToList();
            var merchantIds = orders.Where(o => o.MerchantId.HasValue).Select(o => o.MerchantId!.Value).Distinct().ToList();
            var rates = (await _merchantRepo.GetListAsync(m => merchantIds.Contains(m.Id))).ToDictionary(m => m.Id, m => m.CommissionRate);
            var pays = await AsyncExecuter.ToListAsync((await _paymentRepo.GetQueryableAsync())
                .Where(p => ids.Contains(p.OrderId) && p.Purpose == PaymentPurpose.Order && p.CompletedAt != null &&
                            (p.Status == PaymentTransactionStatus.Succeeded || p.Status == PaymentTransactionStatus.Refunded))
                .Select(p => new { p.OrderId, p.Amount, p.SettlementId }));
            var refundSettled = (await AsyncExecuter.ToListAsync((await _refundRepo.GetQueryableAsync())
                .Where(r => ids.Contains(r.OrderId) && r.SettlementId != null).Select(r => r.OrderId))).ToHashSet();
            var assigned = (await AsyncExecuter.ToListAsync((await _timelineRepo.GetQueryableAsync())
                .Where(t => ids.Contains(t.WashOrderId) && t.Note != null && t.Note.Contains("Admin phân công")).Select(t => t.WashOrderId))).ToHashSet();
            return orders.Select(o => new CommissionReviewItemDto
            {
                OrderId = o.Id, OrderCode = o.OrderCode, MerchantId = o.MerchantId, MerchantName = o.MerchantName,
                AppliedRate = o.CommissionRateApplied, MerchantCurrentRate = o.MerchantId.HasValue && rates.TryGetValue(o.MerchantId.Value, out var r) ? r : null,
                Source = o.CommissionSnapshotSource, Reason = o.CommissionSnapshotSource == "LegacyUnverified" ? "LegacyUnverified" : "Missing",
                LockedAt = o.CommissionLockedAt.HasValue ? DateTime.SpecifyKind(o.CommissionLockedAt.Value, DateTimeKind.Utc) : null,
                CollectedLaundryAmount = pays.Where(p => p.OrderId == o.Id).Sum(p => p.Amount),
                InSettlement = pays.Any(p => p.OrderId == o.Id && p.SettlementId != null) || refundSettled.Contains(o.Id),
                HasAssignmentHistory = assigned.Contains(o.Id)
            }).ToList();
        }

        public async Task<BaseResponse<CommissionReviewItemDto>> ReconcileAsync(Guid orderId, ReconcileCommissionCommand command)
        {
            var evidence = command.Evidence?.Trim();
            if (command.Rate is not { } rate || rate < 0 || rate > 100 || decimal.Round(rate, 2) != rate)
                return AdministrativeWriteGuard.Error<CommissionReviewItemDto>(HttpStatusCode.BadRequest, "InvalidRate", "Tỷ lệ phải từ 0 đến 100, tối đa 2 chữ số thập phân.");
            if (string.IsNullOrEmpty(evidence) || evidence.Length is < 10 or > 1000)
                return AdministrativeWriteGuard.Error<CommissionReviewItemDto>(HttpStatusCode.BadRequest, "EvidenceRequired", "Cần ghi căn cứ/bằng chứng (10-1000 ký tự).");
            var payload = new { orderId, rate, evidence, command.ExpectedRate, command.ExpectedMissing };

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<CommissionReviewItemDto>(command.OperationId, CurrentUser.Id!.Value, "Finance", "ReconcileCommission", orderId.ToString(), payload) is { } replay)
                return replay;
            var order = await _orderRepo.GetForUpdateAsync(orderId);
            if (order == null || order.IsDeleted || order.MerchantId == null)
                return NotFound<CommissionReviewItemDto>("Không tìm thấy đơn đã phân công Merchant.");
            if (command.ExpectedMissing ? order.CommissionRateApplied != null : order.CommissionRateApplied != command.ExpectedRate)
                return AdministrativeWriteGuard.Error<CommissionReviewItemDto>(HttpStatusCode.Conflict, "RateChanged", "Tỷ lệ của đơn đã thay đổi kể từ lúc bạn xem; hãy tải lại.");
            var payments = await _paymentRepo.GetListAsync(p => p.OrderId == orderId && p.Purpose == PaymentPurpose.Order);
            var refunds = await _refundRepo.GetListAsync(r => r.OrderId == orderId);
            // Closed periods keep their numbers: a settled order is corrected through a later adjustment, not here.
            if (payments.Any(p => p.SettlementId != null) || refunds.Any(r => r.SettlementId != null))
                return AdministrativeWriteGuard.Error<CommissionReviewItemDto>(HttpStatusCode.Conflict, "AlreadySettled", "Đơn đã có khoản nằm trong kỳ đối soát; không sửa tỷ lệ tại đây.");

            var before = new { order.CommissionRateApplied, order.CommissionSnapshotSource };
            order.CommissionRateApplied = rate;
            order.CommissionSnapshotSource = "AdminReconciled";
            order.CommissionLockedAt ??= DateTime.UtcNow;
            await _orderRepo.UpdateAsync(order, autoSave: true);
            foreach (var p in payments.Where(p => p.SettlementId == null))
            {
                p.CommissionRateSnapshot = rate; p.MerchantIdSnapshot ??= order.MerchantId; p.FinancialSnapshotBasis = "admin-reconciled";
            }
            foreach (var r in refunds.Where(r => r.SettlementId == null))
            {
                r.CommissionRateSnapshot = rate; r.MerchantIdSnapshot ??= order.MerchantId; r.FinancialSnapshotBasis = "admin-reconciled";
            }
            if (payments.Count > 0) await _paymentRepo.UpdateManyAsync(payments, autoSave: true);
            if (refunds.Count > 0) await _refundRepo.UpdateManyAsync(refunds, autoSave: true);
            await Audit.LogAsync(new AuditEntry("Finance", "ReconcileCommission", "Order", order.Id.ToString(), order.OrderCode)
            {
                Reason = evidence, ChangedFields = ["commissionRateApplied", "commissionSnapshotSource"],
                Details = new { before, after = new { order.CommissionRateApplied, order.CommissionSnapshotSource }, merchantId = order.MerchantId }
            });
            var response = Success((await ToItemsAsync([order]))[0], "Đã đối chiếu tỷ lệ hoa hồng của đơn.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Finance", "ReconcileCommission", orderId.ToString(), payload, response);
            await uow.CompleteAsync();
            return response;
        }
    }
}
