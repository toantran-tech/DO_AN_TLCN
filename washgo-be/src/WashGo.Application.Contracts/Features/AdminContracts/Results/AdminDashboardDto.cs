using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AdminContracts.Results
{
    /// <summary>Giá trị kỳ này so với kỳ trước cùng độ dài. previous/changePercent null khi kỳ = all.</summary>
    public class AdminKpiDto
    {
        public decimal Current { get; set; }
        public decimal? Previous { get; set; }
        /// <summary>null khi kỳ trước = 0 mà kỳ này &gt; 0 (xem isNew) hoặc khi không có kỳ so sánh; không giả lập +100%.</summary>
        public decimal? ChangePercent { get; set; }
        /// <summary>true: kỳ trước = 0, kỳ này phát sinh mới.</summary>
        public bool IsNew { get; set; }
    }

    public class AdminDashboardDto
    {
        /// <summary>Thời điểm chụp số liệu (UTC); mọi số đọc trong cùng một snapshot DB.</summary>
        public DateTime GeneratedAt { get; set; }
        public string Period { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime? PreviousFromDate { get; set; }
        public DateTime? PreviousToDate { get; set; }
        public AdminCustomerStatsDto Customers { get; set; } = new();
        public AdminMerchantStatsDto Merchants { get; set; } = new();
        public AdminOrderStatsDto Orders { get; set; } = new();
        public AdminRevenueStatsDto Revenue { get; set; } = new();
        public AdminLockerStatsDto Lockers { get; set; } = new();
        public List<AdminTopMerchantDto> TopMerchants { get; set; } = [];
        /// <summary>Định nghĩa từng chỉ số (để FE hiển thị chú thích).</summary>
        public Dictionary<string, string> Definitions { get; set; } = [];
    }

    public class AdminCustomerStatsDto
    {
        /// <summary>Tài khoản Customer đang hoạt động (không tính tài khoản bị khóa).</summary>
        public int Total { get; set; }
        public int Locked { get; set; }
        /// <summary>Customer đăng ký trong kỳ (cả đã khóa sau đó).</summary>
        public AdminKpiDto New { get; set; } = new();
    }

    public class AdminMerchantStatsDto
    {
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Suspended { get; set; }
        public AdminKpiDto New { get; set; } = new();
    }

    public class AdminOrderStatsDto
    {
        /// <summary>Đơn tạo trong kỳ.</summary>
        public AdminKpiDto Created { get; set; } = new();
        /// <summary>Đơn chuyển sang Hoàn thành trong kỳ (theo lịch sử trạng thái, không theo ngày tạo).</summary>
        public AdminKpiDto Completed { get; set; } = new();
        /// <summary>Đơn bị hủy trong kỳ (theo thời điểm hủy).</summary>
        public AdminKpiDto Cancelled { get; set; } = new();
        /// <summary>Hiện tại (không phụ thuộc kỳ): đơn đã gửi chờ phân công Merchant.</summary>
        public int WaitingAssignment { get; set; }
        /// <summary>Hiện tại: đơn chờ phân công quá 60 phút.</summary>
        public int WaitingAssignmentOverdue { get; set; }
        /// <summary>Hiện tại: đơn đang được xử lý (từ lấy đồ tới chờ trả).</summary>
        public int InProgress { get; set; }
        /// <summary>Hiện tại: số đơn theo từng trạng thái.</summary>
        public Dictionary<WashOrderStatus, int> CurrentByStatus { get; set; } = [];
    }

    /// <summary>Theo thời điểm giao dịch (cùng công thức /finance/revenue-report): tổng thu trước chi trả Merchant, không phải lợi nhuận.</summary>
    public class AdminRevenueStatsDto
    {
        /// <summary>Tiền giặt + phí lưu kho/quá hạn + phí hủy đã thu − tiền đã hoàn.</summary>
        public AdminKpiDto Total { get; set; } = new();
        public decimal LaundryRevenue { get; set; }
        public decimal StorageFeeRevenue { get; set; }
        public decimal CancellationFeeRevenue { get; set; }
        public decimal RefundedAmount { get; set; }
    }

    public class AdminLockerStatsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Maintenance { get; set; }
        public int Inactive { get; set; }
        public int TotalBoxes { get; set; }
        public int AvailableBoxes { get; set; }
        /// <summary>Locker hoạt động còn ≤ 10% ô trống (gồm cả hết ô).</summary>
        public int NearlyFull { get; set; }
    }

    /// <summary>
    /// Xếp hạng: số đơn hoàn thành trong kỳ giảm dần, rồi tỷ lệ hoàn thành, rồi Id.
    /// completionRate = completed / (completed + cancelled) × 100 với cancelled = đơn đã phân công cho Merchant bị hủy trong kỳ;
    /// null khi mẫu số = 0. rating = trung bình sao các đánh giá công khai (toàn thời gian), kèm ratingCount.
    /// </summary>
    public class AdminTopMerchantDto
    {
        public Guid MerchantId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public MerchantStatus Status { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        /// <summary>Mẫu số của completionRate.</summary>
        public int FinishedOrders { get; set; }
        public decimal? CompletionRate { get; set; }
        public decimal? Rating { get; set; }
        public int RatingCount { get; set; }
        /// <summary>Tiền giặt đã thu trong kỳ (trước hoàn tiền) của Merchant.</summary>
        public decimal LaundryRevenue { get; set; }
    }
}
