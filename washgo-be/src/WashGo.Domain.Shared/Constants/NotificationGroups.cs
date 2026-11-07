using System;
using System.Collections.Generic;
using System.Linq;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Shared.Constants
{
    /// <summary>Nhóm hiển thị của thông báo để Customer lọc/đếm: đơn hàng, thanh toán, khuyến mãi, phản hồi, hệ thống.</summary>
    public static class NotificationGroups
    {
        public const string Order = "order";
        public const string Payment = "payment";
        public const string Promotion = "promotion";
        public const string Feedback = "feedback";
        public const string System = "system";

        public static readonly IReadOnlyList<string> All = [Order, Payment, Promotion, Feedback, System];

        private static readonly NotificationType[] PaymentTypes =
        [
            NotificationType.AwaitingPayment, NotificationType.PaymentSucceeded, NotificationType.PaymentFailed,
            NotificationType.OverdueFeeApplied, NotificationType.RefundRequested,
            NotificationType.SettlementCreated, NotificationType.SettlementPaid
        ];

        private static readonly NotificationType[] FeedbackTypes =
            [NotificationType.IncidentProcessed, NotificationType.NewIncident, NotificationType.NewRating];

        private static readonly NotificationType[] SystemTypes =
        [
            NotificationType.ServiceProposalReviewed, NotificationType.MerchantProfileReviewed,
            NotificationType.MerchantStatusChanged, NotificationType.OverdueSummary,
            NotificationType.ServiceProposalSubmitted, NotificationType.MerchantProfileUpdateRequested
        ];

        public static string Of(NotificationType type) =>
            PaymentTypes.Contains(type) ? Payment
            : FeedbackTypes.Contains(type) ? Feedback
            : SystemTypes.Contains(type) ? System
            : Order;

        /// <summary>Loại thông báo thuộc một nhóm; nhóm chưa có loại nào (khuyến mãi) trả danh sách rỗng.</summary>
        public static NotificationType[] TypesOf(string group) =>
            Enum.GetValues<NotificationType>().Where(x => string.Equals(Of(x), group, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(group, Promotion, StringComparison.OrdinalIgnoreCase)).ToArray();

        /// <summary>Khóa mẫu mặc định cho thông báo chưa có khóa riêng (vd. "notification.paymentSucceeded").</summary>
        public static string DefaultKey(NotificationType type)
        {
            var name = type.ToString();
            return $"notification.{char.ToLowerInvariant(name[0])}{name[1..]}";
        }
    }
}
