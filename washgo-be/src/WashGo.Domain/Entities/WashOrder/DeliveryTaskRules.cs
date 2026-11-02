using System.Linq;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    public static class DeliveryTaskRules
    {
        /// <summary>Đơn đang có nhiệm vụ lấy/trả chưa hoàn tất của Shipper được gán.</summary>
        public static IQueryable<WashOrder> ActiveDeliveries(IQueryable<WashOrder> orders) =>
            orders.Where(x => !x.IsDeleted && x.ShipperId != null &&
                (x.Status == WashOrderStatus.Deposited && x.AssignedTaskType == DeliveryTaskType.Collect ||
                 x.Status == WashOrderStatus.Paid && x.AssignedTaskType == DeliveryTaskType.Return ||
                 x.Status == WashOrderStatus.Cancelled && x.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant &&
                    x.AssignedTaskType == DeliveryTaskType.Return));
    }
}
