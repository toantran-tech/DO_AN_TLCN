using System;

namespace WashGo.Application.Contracts.Features.LockerContracts.Results
{
    /// <summary>
    /// Thống kê toàn bộ locker được gán cho Merchant (Admin: toàn hệ thống hoặc theo merchantId), một lần đếm,
    /// không theo bộ lọc/trang của bảng. Locker dùng chung nhiều Merchant chỉ tính một lần.
    /// </summary>
    public class LockerSummaryDto
    {
        public int TotalLockers { get; set; }
        public int ActiveLockers { get; set; }
        public int MaintenanceLockers { get; set; }
        public int InactiveLockers { get; set; }
        /// <summary>Số ô chưa xóa của các locker trên, đếm trực tiếp theo trạng thái ô.</summary>
        public int TotalBoxes { get; set; }
        public int AvailableBoxes { get; set; }
        public int OccupiedBoxes { get; set; }
        public int ReservedBoxes { get; set; }
        public int MaintenanceBoxes { get; set; }
        /// <summary>Thời điểm lấy số liệu (UTC).</summary>
        public DateTime GeneratedAt { get; set; }
    }
}
