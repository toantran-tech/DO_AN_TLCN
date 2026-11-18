namespace WashGo.Domain.Shared.Enums
{
    /// <summary>
    /// Trạng thái vòng đời đơn. Số không theo thứ tự nghiệp vụ: 16-19 được thêm sau, thứ tự hiển thị lấy từ
    /// <see cref="WashOrderStatusFlow.BusinessOrder"/>.
    /// </summary>
    public enum WashOrderStatus
    {
        Pending = 1,
        Deposited = 2,
        Collected = 3,
        Washing = 4,
        Drying = 5,
        Ironing = 6,
        Folding = 7,
        AwaitingPayment = 8,
        Paid = 9,
        /// <summary>Shipper đã đặt đồ vào ô locker trả; khách đến nhận (PickupDeadline bắt đầu từ đây).</summary>
        ReadyToReturn = 10,
        Returned = 11,
        Completed = 12,
        MovedToStorage = 13,
        TransferringToStorage = 14,
        ReturningFromStorage = 15,
        /// <summary>Admin đã phân công tiệm giặt; chờ Merchant phân công Shipper lấy đồ.</summary>
        MerchantAssigned = 16,
        /// <summary>Merchant đã phân công Shipper lấy đồ tại locker gửi; chờ Shipper thu gom.</summary>
        CollectionShipperAssigned = 17,
        /// <summary>Merchant xác nhận đồ đã chuẩn bị xong tại tiệm; chờ phân công Shipper trả đồ.</summary>
        ReadyForReturnDispatch = 18,
        /// <summary>Merchant đã phân công Shipper trả đồ; chờ Shipper đặt đồ vào locker.</summary>
        ReturnShipperAssigned = 19,
        Cancelled = 99
    }

    /// <summary>Nhóm trạng thái và thứ tự nghiệp vụ dùng chung cho luật ghi, đếm, SLA và tiến độ.</summary>
    public static class WashOrderStatusFlow
    {
        /// <summary>Đồ còn trong ô locker gửi, chưa được Shipper thu gom.</summary>
        public static readonly WashOrderStatus[] AtDepositLocker =
            [WashOrderStatus.Deposited, WashOrderStatus.MerchantAssigned, WashOrderStatus.CollectionShipperAssigned];

        /// <summary>Đã thanh toán, đồ còn ở tiệm chờ trả về locker.</summary>
        public static readonly WashOrderStatus[] PaidAtMerchant =
            [WashOrderStatus.Paid, WashOrderStatus.ReadyForReturnDispatch, WashOrderStatus.ReturnShipperAssigned];

        /// <summary>Thứ tự nghiệp vụ của luồng chính (không sắp theo số enum).</summary>
        public static readonly WashOrderStatus[] BusinessOrder =
        [
            WashOrderStatus.Pending, WashOrderStatus.Deposited, WashOrderStatus.MerchantAssigned,
            WashOrderStatus.CollectionShipperAssigned, WashOrderStatus.Collected, WashOrderStatus.Washing,
            WashOrderStatus.Drying, WashOrderStatus.Ironing, WashOrderStatus.Folding, WashOrderStatus.AwaitingPayment,
            WashOrderStatus.Paid, WashOrderStatus.ReadyForReturnDispatch, WashOrderStatus.ReturnShipperAssigned,
            WashOrderStatus.ReadyToReturn, WashOrderStatus.TransferringToStorage, WashOrderStatus.MovedToStorage,
            WashOrderStatus.ReturningFromStorage, WashOrderStatus.Returned, WashOrderStatus.Completed,
            WashOrderStatus.Cancelled
        ];

        public static bool IsAtDepositLocker(WashOrderStatus status) =>
            status is WashOrderStatus.Deposited or WashOrderStatus.MerchantAssigned or WashOrderStatus.CollectionShipperAssigned;

        public static bool IsPaidAtMerchant(WashOrderStatus status) =>
            status is WashOrderStatus.Paid or WashOrderStatus.ReadyForReturnDispatch or WashOrderStatus.ReturnShipperAssigned;
    }
}
