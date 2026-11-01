namespace WashGo.Domain.Shared.Enums
{
    /// <summary>Loại thông báo theo bảng thông báo real-time trong tài liệu tổng hợp chức năng.</summary>
    public enum NotificationType
    {
        OrderCreated = 1,
        Deposited = 2,
        OrderAccepted = 3,
        OrderProcessing = 4,
        AwaitingPayment = 5,
        PaymentSucceeded = 6,
        PaymentFailed = 7,
        ReadyForPickup = 8,
        PickedUp = 9,
        OrderCancelled = 10,
        MovedToStorage = 11,
        IncidentProcessed = 12,
        NewOrderForMerchant = 20,
        NewTaskForShipper = 30,
        ShipperCollected = 31,
        ShipperReturned = 32,
        NewIncident = 40,
        RefundRequested = 41,
        NewRating = 42,
        CustomerDeposited = 43,
        CustomerPickedUp = 44
    }
}
