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

        /// <summary>Mốc bắt đầu của kỳ lọc (UTC, hậu tố Z; FE đổi sang giờ Việt Nam).</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Mốc kết thúc của kỳ lọc (UTC, hậu tố Z; FE đổi sang giờ Việt Nam).</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>Tổng số đơn gán cho tiệm trong kỳ lọc.</summary>
        public int TotalOrders { get; set; }

        /// <summary>Thống kê số lượng đơn theo từng trạng thái của WashOrderStatus trong kỳ lọc.</summary>
        public MerchantStatusCountsDto StatusCounts { get; set; } = new();

        /// <summary>5 đơn hàng mới nhất cần tiệm xử lý.</summary>
        public List<WashOrderResultDto> LatestOrders { get; set; } = [];

        /// <summary>Tối đa 4 kỳ đối soát tài chính gần nhất của tiệm.</summary>
        public List<SettlementDto> RecentSettlements { get; set; } = [];

        /// <summary>Thời điểm tạo snapshot trên server (UTC, hậu tố Z; FE đổi sang giờ Việt Nam).</summary>
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Số lượng đơn phân bố theo từng trạng thái của WashOrderStatus; tổng các trạng thái (và tổng các nhóm) bằng tổng đơn.
    /// </summary>
    public class MerchantStatusCountsDto
    {
        public int Pending { get; set; }
        public int Deposited { get; set; }
        /// <summary>Đã phân công tiệm, chờ Merchant phân công Shipper lấy đồ (16).</summary>
        public int MerchantAssigned { get; set; }
        /// <summary>Đã có Shipper lấy đồ, chờ thu gom (17).</summary>
        public int CollectionShipperAssigned { get; set; }
        public int Collected { get; set; }
        public int Washing { get; set; }
        public int Drying { get; set; }
        public int Ironing { get; set; }
        public int Folding { get; set; }
        public int AwaitingPayment { get; set; }
        public int Paid { get; set; }
        /// <summary>Tiệm đã chuẩn bị xong, chờ phân công Shipper trả đồ (18).</summary>
        public int ReadyForReturnDispatch { get; set; }
        /// <summary>Đã có Shipper trả đồ, chờ đặt vào locker (19).</summary>
        public int ReturnShipperAssigned { get; set; }
        public int ReadyToReturn { get; set; }
        public int Returned { get; set; }
        public int Completed { get; set; }
        public int TransferringToStorage { get; set; }
        public int ReturningFromStorage { get; set; }
        public int MovedToStorage { get; set; }
        public int Cancelled { get; set; }

        /// <summary>
        /// Chuyển thành Dictionary key-value để frontend dễ dàng duyệt hoặc vẽ chart.
        /// </summary>
        public Dictionary<string, int> ByStatus => new()
        {
            ["Pending"] = Pending,
            ["Deposited"] = Deposited,
            ["MerchantAssigned"] = MerchantAssigned,
            ["CollectionShipperAssigned"] = CollectionShipperAssigned,
            ["Collected"] = Collected,
            ["Washing"] = Washing,
            ["Drying"] = Drying,
            ["Ironing"] = Ironing,
            ["Folding"] = Folding,
            ["AwaitingPayment"] = AwaitingPayment,
            ["Paid"] = Paid,
            ["ReadyForReturnDispatch"] = ReadyForReturnDispatch,
            ["ReturnShipperAssigned"] = ReturnShipperAssigned,
            ["ReadyToReturn"] = ReadyToReturn,
            ["Returned"] = Returned,
            ["Completed"] = Completed,
            ["TransferringToStorage"] = TransferringToStorage,
            ["MovedToStorage"] = MovedToStorage,
            ["ReturningFromStorage"] = ReturningFromStorage,
            ["Cancelled"] = Cancelled
        };

        /// <summary>
        /// Nhóm vận hành không trùng nhau: Incoming = đã gửi/đã phân công tiệm/đã phân công lấy đồ; ReturnPreparation = Paid
        /// chờ tiệm chuẩn bị; ReturnDispatch = sẵn sàng trả/đã phân công trả; AwaitingPickup = đồ đã ở locker trả.
        /// </summary>
        public Dictionary<string, int> ByGroup => new()
        {
            ["Pending"] = Pending,
            ["Incoming"] = Deposited + MerchantAssigned + CollectionShipperAssigned,
            ["Processing"] = Collected + Washing + Drying + Ironing + Folding,
            ["AwaitingPayment"] = AwaitingPayment,
            ["ReturnPreparation"] = Paid,
            ["ReturnDispatch"] = ReadyForReturnDispatch + ReturnShipperAssigned,
            ["AwaitingPickup"] = ReadyToReturn,
            ["Storage"] = TransferringToStorage + MovedToStorage + ReturningFromStorage,
            ["Finished"] = Returned + Completed,
            ["Cancelled"] = Cancelled
        };
    }
}

