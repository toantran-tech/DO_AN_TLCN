using System;
using System.Collections.Generic;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results
{
    /// <summary>
    /// Dữ liệu tổng quan Merchant Dashboard đồng bộ trong 1 snapshot duy nhất (UC 2.2.1).
    /// </summary>
    public class MerchantDashboardDto
    {
        /// <summary>Kỳ lọc áp dụng: "today", "week", "month", "custom", "all".</summary>
        public string Period { get; set; } = "all";

        /// <summary>Mốc bắt đầu của kỳ lọc (theo giờ VN UTC+7).</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Mốc kết thúc của kỳ lọc (theo giờ VN UTC+7).</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>Tổng số đơn gán cho tiệm trong kỳ lọc.</summary>
        public int TotalOrders { get; set; }

        /// <summary>Thống kê số lượng đơn theo 14 trạng thái của WashOrderStatus trong kỳ lọc.</summary>
        public MerchantStatusCountsDto StatusCounts { get; set; } = new();

        /// <summary>5 đơn hàng mới nhất cần tiệm xử lý.</summary>
        public List<WashOrderResultDto> LatestOrders { get; set; } = [];

        /// <summary>Tối đa 4 kỳ đối soát tài chính gần nhất của tiệm.</summary>
        public List<SettlementDto> RecentSettlements { get; set; } = [];

        /// <summary>Thời điểm tạo snapshot trên server (theo giờ VN UTC+7).</summary>
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Số lượng đơn phân bố theo 14 trạng thái xử lý trong hệ thống WashGo.
    /// </summary>
    public class MerchantStatusCountsDto
    {
        public int Pending { get; set; }
        public int Deposited { get; set; }
        public int Collected { get; set; }
        public int Washing { get; set; }
        public int Drying { get; set; }
        public int Ironing { get; set; }
        public int Folding { get; set; }
        public int AwaitingPayment { get; set; }
        public int Paid { get; set; }
        public int ReadyToReturn { get; set; }
        public int Returned { get; set; }
        public int Completed { get; set; }
        public int MovedToStorage { get; set; }
        public int Cancelled { get; set; }

        /// <summary>
        /// Chuyển thành Dictionary key-value để frontend dễ dàng duyệt hoặc vẽ chart.
        /// </summary>
        public Dictionary<string, int> ByStatus => new()
        {
            ["Pending"] = Pending,
            ["Deposited"] = Deposited,
            ["Collected"] = Collected,
            ["Washing"] = Washing,
            ["Drying"] = Drying,
            ["Ironing"] = Ironing,
            ["Folding"] = Folding,
            ["AwaitingPayment"] = AwaitingPayment,
            ["Paid"] = Paid,
            ["ReadyToReturn"] = ReadyToReturn,
            ["Returned"] = Returned,
            ["Completed"] = Completed,
            ["MovedToStorage"] = MovedToStorage,
            ["Cancelled"] = Cancelled
        };
    }
}

