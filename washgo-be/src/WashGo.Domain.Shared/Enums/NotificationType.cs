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
        DepositReminder = 13,
        PickupReminder = 14,
        OverdueFeeApplied = 15,
        /// <summary>Đơn chờ gửi đã quá hạn gửi đồ: khách nên hủy để giải phóng ô (không tự hủy).</summary>
        DepositExpired = 16,
        /// <summary>Trao đổi/bổ sung minh chứng, khách sửa hoặc rút khiếu nại.</summary>
        IncidentMessage = 17,
        NewOrderForMerchant = 20,
        MerchantReturnReminder = 21,
        SlaWarning = 22,
        StorageTransferDue = 23,
        SettlementCreated = 24,
        SettlementPaid = 25,
        ServiceProposalReviewed = 26,
        MerchantProfileReviewed = 27,
        MerchantStatusChanged = 29,
        NewTaskForShipper = 30,
        ShipperCollected = 31,
        ShipperReturned = 32,
        ShipperCollectReminder = 33,
        ShipperReturnReminder = 34,
        NewIncident = 40,
        RefundRequested = 41,
        NewRating = 42,
        CustomerDeposited = 43,
        CustomerPickedUp = 44,
        OverdueSummary = 45,
        ServiceProposalSubmitted = 46,
        MerchantProfileUpdateRequested = 47,
        RefundReviewed = 48,
        RefundConfirmed = 49
    }
}
