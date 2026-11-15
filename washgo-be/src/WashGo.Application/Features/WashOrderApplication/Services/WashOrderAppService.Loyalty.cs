using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services;

public partial class WashOrderAppService
{
    private async Task<(decimal Cash, long Points, Guid? PaymentId)> CancellationAllocationAsync(WashOrder order, decimal fee)
    {
        var payments = await _payments.GetListAsync(x => x.OrderId == order.Id && x.Purpose == PaymentPurpose.Order &&
            !x.RequiresReconciliation && (x.Status == PaymentTransactionStatus.Succeeded || x.Status == PaymentTransactionStatus.Refunded));
        var payment = payments.FirstOrDefault(x => x.LoyaltyApplied);
        if (payment == null)
            return (Math.Max(0, payments.Sum(x => x.Amount) +
                (payments.Count == 0 && order.PaymentStatus == PaymentStatus.Paid && order.PaymentMethod == PaymentMethod.Cash ? order.FinalAmount : 0) - fee), 0, payments.FirstOrDefault()?.Id);
        var refund = LoyaltyMath.CancellationRefund(payment.Amount, payment.PointsToRedeem, fee);
        return (refund.Cash, refund.Points, payment.Id);
    }
}
