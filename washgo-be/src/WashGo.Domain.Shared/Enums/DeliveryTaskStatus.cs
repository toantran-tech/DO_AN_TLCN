namespace WashGo.Domain.Shared.Enums
{
    /// <summary>Trạng thái một nhiệm vụ lấy/trả đồ đã giao cho Shipper.</summary>
    public enum DeliveryTaskStatus
    {
        /// <summary>Đã phân công, chưa hoàn tất.</summary>
        Assigned = 1,
        /// <summary>Shipper đã lấy đồ khỏi tủ (Collect) hoặc đã đặt đồ vào tủ (Return).</summary>
        Completed = 2,
        /// <summary>Đơn được phân công lại cho người/nhiệm vụ khác trước khi hoàn tất.</summary>
        Reassigned = 3,
        /// <summary>Đơn bị hủy khi nhiệm vụ chưa hoàn tất.</summary>
        Cancelled = 4
    }
}
