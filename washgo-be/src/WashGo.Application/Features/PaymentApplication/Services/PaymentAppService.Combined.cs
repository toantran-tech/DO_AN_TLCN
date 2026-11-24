using System.Net;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService
{
    [UnitOfWork(IsDisabled = true)]
    public Task<BaseResponse<PaymentResultDto>> CreateCombinedPaymentAsync(CreatePaymentCommand command) =>
        CreateSessionAsync(command, PaymentPurpose.Combined);

    public async Task<BaseResponse<OrderPaymentSummaryDto>> GetOrderPaymentSummaryAsync(Guid orderId, long pointsToRedeem)
    {
        if (!CustomerSignedIn)
            return new() { StatusCode = HttpStatusCode.Forbidden, Message = "Chỉ Customer được xem bảng kê của mình." };
        if (pointsToRedeem < 0)
            return BadRequest<OrderPaymentSummaryDto>("Số điểm không được âm.");
        var order = await _orders.FindAsync(orderId);
        if (order == null || order.IsDeleted || order.CustomerId != CurrentUser.Id)
            return NotFound<OrderPaymentSummaryDto>("Không tìm thấy đơn hàng của bạn.");
        return Success(await BuildOrderPaymentSummaryAsync(order, pointsToRedeem));
    }

    private async Task<OrderPaymentSummaryDto> BuildOrderPaymentSummaryAsync(WashOrder order, long points = 0)
    {
        var blockers = await BlockersAsync(order.Id, PaymentPurpose.Combined, includeOverlaps: true);
        var summary = await BuildObligationsAsync(order, DateTime.UtcNow);
        summary.Quote = await BuildQuoteAsync(order, PaymentPurpose.Combined, points, blockers, summary);
        summary.RequiresReconciliation = blockers.Count > 1 || blockers.Any(x => x.RequiresReconciliation);
        if (order.ActiveStorageCycleId is Guid cycleId && order.Status == WashOrderStatus.MovedToStorage)
        {
            var requests = LazyServiceProvider.LazyGetRequiredService<IRepository<StorageReturnRequest, Guid>>();
            var request = await requests.FirstOrDefaultAsync(x => x.CycleId == cycleId &&
                (x.State == StorageReturnState.Requested || x.State == StorageReturnState.Assigned || x.State == StorageReturnState.InTransit));
            summary.ReturnLockerId = request?.LockerId ?? summary.ReturnLockerId;
        }
        var refunds = await _refundRequestRepo.GetListAsync(x => !x.IsDeleted && x.OrderId == order.Id &&
            x.Status != RefundRequestStatus.Rejected);
        var pending = refunds.Where(x => x.Status is RefundRequestStatus.Pending or RefundRequestStatus.Approved).ToList();
        var refunded = refunds.Where(x => x.Status == RefundRequestStatus.Refunded).ToList();
        summary.Refunds = new(pending.Sum(x => x.Amount), refunded.Sum(x => x.Amount),
            pending.Sum(x => x.PointsAmount), refunded.Sum(x => x.PointsAmount));
        if (order.Status == WashOrderStatus.Cancelled)
        {
            summary.CancellationRefundAmount = order.CancellationRefundAmount;
            summary.CancellationRefundRemaining = Math.Max(0m, order.CancellationRefundAmount -
                pending.Concat(refunded).Where(x => !x.IsExcessPayment).Sum(x => x.Amount));
        }
        return summary;
    }

    private async Task<OrderPaymentSummaryDto> BuildObligationsAsync(WashOrder order, DateTime now)
    {
        var cancelled = order.Status == WashOrderStatus.Cancelled;
        var laundryDue = await PaymentDueAsync(order, PaymentPurpose.Order);
        var laundry = !cancelled && (order.PaymentStatus == PaymentStatus.Paid || laundryDue.Error == null)
            ? Math.Max(0m, order.FinalAmount) : 0m;
        var settings = await _settings.ForOrderAsync(order);
        var storage = cancelled
            ? Math.Max(order.OverdueFeeTotal, order.AccumulatedStorageFee)
            : OverdueFeeCalculator.GetTotalStorageDue(order, now, settings);
        var canPayStorage = cancelled || order.Status is WashOrderStatus.ReadyToReturn or WashOrderStatus.MovedToStorage
            or WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage;
        if (!canPayStorage) storage = order.OverdueFeeTotal;
        var cancellation = cancelled ? order.CancelFeeAmount : 0m;
        var summary = new OrderPaymentSummaryDto
        {
            OrderId = order.Id, OrderCode = order.OrderCode, OrderStatus = order.Status,
            ReturnLockerId = order.ReturnLockerId ?? (order.Status == WashOrderStatus.MovedToStorage ? order.LockerId : null),
            ComputedAt = now, QuoteExpiresAt = now.AddMinutes(5)
        };
        AddObligation(summary, PaymentPurpose.Order, laundry, order.PaymentStatus == PaymentStatus.Paid ? laundry : 0m);
        AddObligation(summary, PaymentPurpose.OverdueStorageFee, storage, order.StorageFeeTotal, canPayStorage);
        AddObligation(summary, PaymentPurpose.CancellationFee, cancellation, order.CancelFeePaidAmount);
        summary.Outstanding = summary.Obligations.Sum(x => x.Outstanding);
        summary.Settled = summary.Outstanding == 0m && (cancelled || order.PaymentStatus == PaymentStatus.Paid);
        if (order.Status == WashOrderStatus.MovedToStorage)
        {
            summary.CanRequestStorageReturn = order.StorageWorkflowVersion >= 1 && order.MovedToStorageAt.HasValue &&
                StoragePaymentSatisfied(order, storage);
            summary.StorageReturnReason = summary.CanRequestStorageReturn ? null :
                order.StorageWorkflowVersion < 1 ? "LegacyStorageWorkflow" :
                !order.MovedToStorageAt.HasValue ? "StorageDataMissing" : "PaymentRequired";
        }
        if (cancelled)
        {
            var basis = order.CancellationFromStatus switch
            {
                WashOrderStatus.Collected => order.FinalAmount,
                WashOrderStatus.Pending or WashOrderStatus.Deposited or WashOrderStatus.MerchantAssigned or
                    WashOrderStatus.CollectionShipperAssigned => order.TotalAmount,
                _ => (decimal?)null
            };
            summary.CancellationBasis = order.CancellationFeeBasis ?? basis;
            summary.CancellationRatePercent = order.CancellationFeePercent;
            if (summary.CancellationRatePercent == null && summary.CancellationBasis is > 0m)
            {
                var originalBasis = summary.CancellationBasis.Value;
                var inferred = decimal.Round(cancellation * 100m / originalBasis, 0, MidpointRounding.AwayFromZero);
                if (decimal.Round(originalBasis * inferred / 100m, 2, MidpointRounding.AwayFromZero) == cancellation)
                    summary.CancellationRatePercent = inferred;
            }
            var fees = await _transactionRepo.GetListAsync(x => x.OrderId == order.Id && !x.IsDeleted &&
                !x.RequiresReconciliation && x.Status == PaymentTransactionStatus.Succeeded &&
                (x.Purpose == PaymentPurpose.CancellationFee || x.Purpose == PaymentPurpose.Combined));
            summary.CancellationOffset = Math.Max(0m, order.CancelFeePaidAmount - fees.Sum(x => x.GetCancellationCash()));
        }
        return summary;
    }

    private static void AddObligation(OrderPaymentSummaryDto summary, PaymentPurpose purpose, decimal due,
        decimal paid, bool payable = true)
    {
        if (due > 0m || paid > 0m)
            summary.Obligations.Add(new(purpose, due, paid, payable ? Math.Max(0m, due - paid) : 0m));
    }

    internal static bool StoragePaymentSatisfied(WashOrder order, decimal storageDue) =>
        order.PaymentStatus == PaymentStatus.Paid && order.StorageFeeTotal >= storageDue;

    private static decimal OutstandingOf(OrderPaymentSummaryDto summary, PaymentPurpose purpose) =>
        summary.Obligations.FirstOrDefault(x => x.Purpose == purpose)?.Outstanding ?? 0m;

    private static List<PaymentAllocationDto> DescribeAllocations(PaymentTransaction transaction)
    {
        var allocations = new List<PaymentAllocationDto>(3);
        if (transaction.GetLaundryCash() > 0m || transaction.PointsToRedeem > 0)
            allocations.Add(new(PaymentPurpose.Order, transaction.GetLaundryCash(), transaction.PointsToRedeem));
        if (transaction.GetStorageCash() > 0m)
            allocations.Add(new(PaymentPurpose.OverdueStorageFee, transaction.GetStorageCash(), 0));
        if (transaction.GetCancellationCash() > 0m)
            allocations.Add(new(PaymentPurpose.CancellationFee, transaction.GetCancellationCash(), 0));
        return allocations;
    }

    private static bool CombinedAllocationApplies(WashOrder order, PaymentTransaction transaction, OrderPaymentSummaryDto summary) =>
        transaction.Amount > 0m && transaction.GetLaundryCash() >= 0m && transaction.StorageAmount >= 0m &&
        transaction.CancellationAmount >= 0m &&
        transaction.Amount == transaction.GetLaundryCash() + transaction.StorageAmount + transaction.CancellationAmount &&
        (transaction.LaundryAmount == 0m || CanMarkOrderPaid(order, transaction)) &&
        transaction.StorageAmount <= OutstandingOf(summary, PaymentPurpose.OverdueStorageFee) &&
        transaction.CancellationAmount <= OutstandingOf(summary, PaymentPurpose.CancellationFee);

    private async Task<bool> ApplyCombinedPaymentAsync(WashOrder order, PaymentTransaction transaction, string provider,
        string? providerTransactionId)
    {
        var summary = await BuildObligationsAsync(order, DateTime.UtcNow);
        if (!CombinedAllocationApplies(order, transaction, summary)) return false;

        if (transaction.LaundryAmount > 0m)
            await MarkOrderPaidAsync(order, transaction, provider, providerTransactionId);
        if (transaction.StorageAmount > 0m)
        {
            order.OverdueFeeTotal = summary.Obligations.Single(x => x.Purpose == PaymentPurpose.OverdueStorageFee).Due;
            order.StorageFeeTotal += transaction.StorageAmount;
            RecordFeePayment(order, PaymentPurpose.OverdueStorageFee, transaction.StorageAmount, provider);
        }
        if (transaction.CancellationAmount > 0m)
        {
            order.CancelFeePaidAmount += transaction.CancellationAmount;
            RecordFeePayment(order, PaymentPurpose.CancellationFee, transaction.CancellationAmount, provider);
        }
        await _orders.UpdateAsync(order, autoSave: true);
        return true;
    }

    private static void RecordFeePayment(WashOrder order, PaymentPurpose purpose, decimal amount, string provider) =>
        order.Timeline.Add(new WashOrderTimeline(Guid.NewGuid())
        {
            EventType = purpose == PaymentPurpose.CancellationFee ? "CancellationFeePaid" : "StorageFeePaid",
            PaymentPurpose = purpose, Amount = amount, WashOrderId = order.Id,
            FromStatus = order.Status, Status = order.Status, ActorRole = "System", ActorName = provider,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
}
