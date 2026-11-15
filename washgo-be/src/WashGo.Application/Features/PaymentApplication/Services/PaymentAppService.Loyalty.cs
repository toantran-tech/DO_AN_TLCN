using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.PaymentApplication.Services;

public partial class PaymentAppService
{
    private static decimal CashCancellationFee(WashOrder order, PaymentTransaction? payment)
    {
        if (payment?.LoyaltyApplied != true) return order.CancelFeePaidAmount;
        var allocation = LoyaltyMath.CancellationRefund(payment.Amount, payment.PointsToRedeem, order.CancelFeePaidAmount);
        return payment.Amount - allocation.Cash;
    }
}
