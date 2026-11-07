namespace WashGo.Domain.Shared.Enums
{
    /// <summary>
    /// Phân loại cảnh báo quá hạn SLA của tiệm giặt (UC 2.2.2).
    /// </summary>
    public enum MerchantSlaAlertType
    {
        /// <summary>Quá hạn lấy đồ từ tủ locker.</summary>
        CollectionOverdue = 1,

        /// <summary>Quá hạn xử lý giặt sấy dự kiến.</summary>
        ProcessingOverdue = 2,

        /// <summary>Quá hạn giao đồ vào tủ locker trả cho khách.</summary>
        ReturnOverdue = 3
    }

    /// <summary>
    /// Mã hành động đề xuất xử lý cho Merchant theo SLA (UC 2.2.2).
    /// </summary>
    public enum MerchantSlaActionType
    {
        /// <summary>Phân công Shipper lấy đồ ngay từ locker.</summary>
        AssignShipperCollect = 1,

        /// <summary>Đẩy nhanh tiến độ giặt sấy và cập nhật trạng thái đơn.</summary>
        SpeedUpProcessing = 2,

        /// <summary>Phân công Shipper giao đồ vào tủ locker trả cho khách.</summary>
        AssignShipperReturn = 3
    }

    /// <summary>
    /// Tiện ích ánh xạ hành động và mô tả chuẩn hóa cho cảnh báo SLA.
    /// </summary>
    public static class MerchantSlaExtensions
    {
        public static MerchantSlaActionType GetSuggestedActionType(this MerchantSlaAlertType alertType) => alertType switch
        {
            MerchantSlaAlertType.CollectionOverdue => MerchantSlaActionType.AssignShipperCollect,
            MerchantSlaAlertType.ProcessingOverdue => MerchantSlaActionType.SpeedUpProcessing,
            MerchantSlaAlertType.ReturnOverdue => MerchantSlaActionType.AssignShipperReturn,
            _ => default
        };

        public static string GetSuggestedActionDescription(this MerchantSlaAlertType alertType) => alertType switch
        {
            MerchantSlaAlertType.CollectionOverdue => "Phân công Shipper lấy đồ ngay từ locker",
            MerchantSlaAlertType.ProcessingOverdue => "Đẩy nhanh tiến độ giặt sấy và cập nhật trạng thái đơn",
            MerchantSlaAlertType.ReturnOverdue => "Phân công Shipper giao đồ vào tủ locker trả cho khách",
            _ => string.Empty
        };

        public static string GetDescription(this MerchantSlaActionType actionType) => actionType switch
        {
            MerchantSlaActionType.AssignShipperCollect => "Phân công Shipper lấy đồ ngay từ locker",
            MerchantSlaActionType.SpeedUpProcessing => "Đẩy nhanh tiến độ giặt sấy và cập nhật trạng thái đơn",
            MerchantSlaActionType.AssignShipperReturn => "Phân công Shipper giao đồ vào tủ locker trả cho khách",
            _ => string.Empty
        };
    }
}
