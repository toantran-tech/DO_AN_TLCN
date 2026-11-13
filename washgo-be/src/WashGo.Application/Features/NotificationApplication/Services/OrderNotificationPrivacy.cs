using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.NotificationApplication.Services;

/// <summary>Customer order notifications, including legacy inbox rows. Does not rewrite the audit/history.</summary>
public static class OrderNotificationPrivacy
{
    private static readonly HashSet<string> PublicArguments = new(StringComparer.Ordinal)
    {
        "orderCode", "lockerName", "boxNumber", "amount", "pickupDeadline", "depositDeadline",
        "depositMinutes", "step", "place", "fee", "hours", "days", "deadline", "paidAmount", "outstandingAmount"
    };

    public static NotificationDto Apply(NotificationDto dto, bool customer = true)
    {
        if (dto.LinkType != "order") return dto;
        dto.MessageArgs = dto.MessageArgs.Where(x => PublicArguments.Contains(x.Key) ||
            !customer && x.Key is "storageLocation" or "storageReference" or "compensationAmount")
            .ToDictionary(x => x.Key, x => x.Value);
        if (customer && (dto.Type == NotificationType.MovedToStorage || dto.MessageKey == "paymentSucceeded.storage.customer"))
        {
            dto.Title = dto.Type == NotificationType.MovedToStorage ? "Thông báo lưu kho" : "Thanh toán thành công";
            dto.Content = "Đồ của bạn đang được WashGo lưu giữ. Vui lòng xem đơn hàng để kiểm tra thanh toán, phí lưu kho và liên hệ hỗ trợ nhận lại đồ.";
            dto.MessageKey = "storage.customer.private";
        }
        return dto;
    }
}
