using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Domain.Shared.Enums;
using System.Linq.Expressions;
using WashGo.Domain.Entities.Notification;

namespace WashGo.Application.Features.NotificationApplication.Services;

/// <summary>Customer order notifications, including legacy inbox rows. Does not rewrite the audit/history.</summary>
public static class OrderNotificationPrivacy
{
    private const string StorageTitle = "Thông báo lưu kho";
    private const string PaymentTitle = "Thanh toán thành công";
    private const string StorageContent = "Đồ của bạn đang được WashGo lưu giữ. Vui lòng xem đơn hàng để kiểm tra thanh toán, phí lưu kho và yêu cầu giao lại locker.";
    private static readonly Expression<Func<Notification, bool>> ProtectedStorage = x =>
        x.LinkType == "order" && (x.Type == NotificationType.MovedToStorage || x.MessageKey == "paymentSucceeded.storage.customer" || x.MessageKey == "storage.customer.private");
    private static readonly ParameterExpression Row = ProtectedStorage.Parameters[0];
    private static readonly Expression SafeTitle = Expression.Condition(ProtectedStorage.Body,
        Expression.Condition(Expression.Equal(Expression.Property(Row, nameof(Notification.Type)), Expression.Constant(NotificationType.MovedToStorage)),
            Expression.Constant(StorageTitle), Expression.Constant(PaymentTitle)), Expression.Property(Row, nameof(Notification.Title)));
    private static readonly Expression SafeContent = Expression.Condition(ProtectedStorage.Body,
        Expression.Constant(StorageContent), Expression.Property(Row, nameof(Notification.Content)));
    private static readonly Func<Notification, bool> IsProtected = ProtectedStorage.Compile();
    private static readonly Func<Notification, string> ReadTitle = Expression.Lambda<Func<Notification, string>>(SafeTitle, Row).Compile();
    private static readonly Func<Notification, string> ReadContent = Expression.Lambda<Func<Notification, string>>(SafeContent, Row).Compile();

    /// <summary>SQL-translatable CASE uses exactly the same visible text as inbox/SignalR projection.</summary>
    public static Expression<Func<Notification, bool>> CustomerMatches(string loweredKeyword)
    {
        Expression Contains(Expression text) => Expression.Call(
            Expression.Call(text, nameof(string.ToLower), Type.EmptyTypes), nameof(string.Contains), Type.EmptyTypes, Expression.Constant(loweredKeyword));
        return Expression.Lambda<Func<Notification, bool>>(Expression.OrElse(Contains(SafeTitle), Contains(SafeContent)), Row);
    }
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
        var source = new Notification { Type = dto.Type, LinkType = dto.LinkType, MessageKey = dto.MessageKey, Title = dto.Title, Content = dto.Content };
        if (customer && IsProtected(source))
        {
            dto.Title = ReadTitle(source);
            dto.Content = ReadContent(source);
            dto.MessageKey = "storage.customer.private";
        }
        return dto;
    }
}
