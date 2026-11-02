using System;
using WashGo.Core.Domain.Attributes;
using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Promotion
{
    [SevagoTable(WashGoDbProperties.Promotions, Schema = WashGoDbProperties.Schema)]
    public class Promotion : BaseEntity
    {
        /// <summary>Số tiền tối thiểu khách còn phải trả sau giảm, để đơn vẫn thanh toán được qua cổng.</summary>
        public const decimal MinPayableAmount = 5000m;

        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsageLimitPerCustomer { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; } = true;

        public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        public PromotionStatus GetStatus(DateTime now)
        {
            if (!IsActive) return PromotionStatus.Inactive;
            if (now < StartAt) return PromotionStatus.Upcoming;
            if (now > EndAt) return PromotionStatus.Expired;
            if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value) return PromotionStatus.Exhausted;
            return PromotionStatus.Active;
        }

        /// <summary>Số tiền giảm trên giá trị đơn, làm tròn đến đồng; đơn sau giảm còn ít nhất MinPayableAmount.</summary>
        public decimal CalculateDiscount(decimal orderAmount)
        {
            if (orderAmount <= 0) return 0m;
            var discount = DiscountType == DiscountType.Percentage
                ? decimal.Round(orderAmount * DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
                : DiscountValue;
            if (MaxDiscountAmount.HasValue) discount = Math.Min(discount, MaxDiscountAmount.Value);
            return Math.Max(0m, Math.Min(discount, orderAmount - MinPayableAmount));
        }

        /// <summary>Trả về lý do không áp dụng được, hoặc null nếu khách dùng được mã cho đơn này.</summary>
        public string? GetIneligibleReason(DateTime now, decimal orderAmount, int customerUsedCount) =>
            GetStatus(now) switch
            {
                PromotionStatus.Inactive => "Mã giảm giá đã ngừng áp dụng.",
                PromotionStatus.Upcoming => "Mã giảm giá chưa đến thời gian áp dụng.",
                PromotionStatus.Expired => "Mã giảm giá đã hết hạn.",
                PromotionStatus.Exhausted => "Mã giảm giá đã hết lượt sử dụng.",
                _ when UsageLimitPerCustomer.HasValue && customerUsedCount >= UsageLimitPerCustomer.Value =>
                    "Bạn đã dùng hết số lượt cho phép của mã này.",
                _ when orderAmount < MinOrderAmount => "Đơn hàng chưa đủ điều kiện áp dụng mã này.",
                _ => null
            };
    }
}
