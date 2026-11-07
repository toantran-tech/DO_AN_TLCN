using System;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>
    /// Thống kê toàn đội Shipper của tiệm, không theo bộ lọc/trang của bảng. Hai góc nhìn độc lập:
    /// làm việc (Working + OnLeave = Total) và vận hành (Available + Busy + Offline = Total).
    /// </summary>
    public class ShipperSummaryDto
    {
        public int Total { get; set; }
        /// <summary>Đang làm việc (shipper.IsActive = true).</summary>
        public int Working { get; set; }
        /// <summary>Tạm nghỉ (shipper.IsActive = false).</summary>
        public int OnLeave { get; set; }
        public int Available { get; set; }
        public int Busy { get; set; }
        public int Offline { get; set; }
        /// <summary>Thời điểm đếm (UTC); mọi số liệu cùng một truy vấn.</summary>
        public DateTime GeneratedAt { get; set; }
    }
}
