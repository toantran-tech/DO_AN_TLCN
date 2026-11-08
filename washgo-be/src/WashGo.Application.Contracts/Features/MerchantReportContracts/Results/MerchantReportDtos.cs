using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.MerchantReportContracts.Results
{
    /// <summary>Báo cáo kinh doanh của Merchant đang đăng nhập; mọi số liệu đọc trong cùng một snapshot.</summary>
    public class MerchantReportDto
    {
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public string Currency { get; set; } = "VND";
        public string Period { get; set; } = "month";
        public string Granularity { get; set; } = "day";
        /// <summary>Khoảng hiện tại [from, toExclusive) theo UTC; dữ liệu tính tới asOf nếu kỳ chưa kết thúc.</summary>
        public ReportRangeDto Current { get; set; } = new();
        /// <summary>Kỳ so sánh liền trước, cùng độ dài và cùng tiến độ (nếu kỳ hiện tại chưa kết thúc).</summary>
        public ReportRangeDto Previous { get; set; } = new();
        /// <summary>true khi kỳ hiện tại chưa kết thúc (ví dụ hôm nay): số liệu chỉ tới asOf.</summary>
        public bool Incomplete { get; set; }
        public DateTime AsOf { get; set; }
        public DateTime GeneratedAt { get; set; }
        public MerchantReportKpisDto Kpis { get; set; } = new();
        public List<ReportBucketDto> Series { get; set; } = [];
        public List<TopServiceDto> TopServices { get; set; } = [];
        /// <summary>Định nghĩa từng chỉ số (để hiển thị/ghi vào file xuất).</summary>
        public Dictionary<string, string> Definitions { get; set; } = [];
    }

    public class ReportRangeDto
    {
        public DateTime From { get; set; }
        public DateTime ToExclusive { get; set; }
        /// <summary>Ngày đầu/cuối theo giờ Việt Nam (yyyy-MM-dd), để hiển thị.</summary>
        public string FromLocalDate { get; set; } = string.Empty;
        public string ToLocalDate { get; set; } = string.Empty;
    }

    /// <summary>Giá trị kỳ này/kỳ trước. changePercent null khi kỳ trước bằng 0 mà kỳ này khác 0 (isNew = true).</summary>
    public class KpiValueDto
    {
        public decimal Current { get; set; }
        public decimal Previous { get; set; }
        public decimal? ChangePercent { get; set; }
        public bool IsNew { get; set; }
        /// <summary>Tạm tính: có khoản thu/hoàn của đơn chưa có tỷ lệ hoa hồng chốt ở kỳ này hoặc kỳ trước.</summary>
        public bool IsEstimated { get; set; }
    }

    /// <summary>Mức bao phủ tỷ lệ hoa hồng chốt của từng kỳ, tính trên mọi khoản thu và khoản hoàn tiền giặt.</summary>
    public class CommissionCoverageDto
    {
        public bool CurrentComplete { get; set; }
        public bool PreviousComplete { get; set; }
        public int CurrentUncoveredOrders { get; set; }
        public int PreviousUncoveredOrders { get; set; }
        /// <summary>Doanh thu thuần của các đơn chưa có tỷ lệ chốt (phần hoa hồng chưa xác định).</summary>
        public decimal CurrentUncoveredNetAmount { get; set; }
        public decimal PreviousUncoveredNetAmount { get; set; }
    }

    /// <summary>Tổng tài chính toàn lịch sử của Merchant (theo ngày giao dịch), cho hồ sơ cửa hàng.</summary>
    public class MerchantLifetimeFinanceDto
    {
        public string Currency { get; set; } = "VND";
        public decimal GrossLaundryRevenue { get; set; }
        public decimal Refunds { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal Commission { get; set; }
        public decimal MerchantPayout { get; set; }
        public decimal FeesCollected { get; set; }
        public int PaidOrderCount { get; set; }
        /// <summary>false: có đơn chưa có tỷ lệ chốt, Commission/MerchantPayout là tạm tính.</summary>
        public bool CommissionComplete { get; set; }
        public int OrdersWithoutCommissionSnapshot { get; set; }
        public decimal UncoveredNetAmount { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>Tỷ lệ có tử số/mẫu số; rate null khi mẫu số 0. changePoints = chênh lệch điểm phần trăm.</summary>
    public class RateKpiDto
    {
        public int Numerator { get; set; }
        public int Denominator { get; set; }
        public decimal? Rate { get; set; }
        public int PreviousNumerator { get; set; }
        public int PreviousDenominator { get; set; }
        public decimal? PreviousRate { get; set; }
        public decimal? ChangePoints { get; set; }
        public string? UnavailableReason { get; set; }
    }

    public class MerchantReportKpisDto
    {
        /// <summary>Tiền giặt khách đã trả cho đơn của tiệm (theo ngày giao dịch thành công).</summary>
        public KpiValueDto GrossLaundryRevenue { get; set; } = new();
        /// <summary>Tiền hoàn cho khách của đơn của tiệm (theo ngày hoàn tiền).</summary>
        public KpiValueDto Refunds { get; set; } = new();
        /// <summary>Doanh thu thuần = tiền giặt − tiền hoàn.</summary>
        public KpiValueDto NetRevenue { get; set; } = new();
        /// <summary>Hoa hồng sàn theo tỷ lệ chốt trên từng đơn (đơn không có tỷ lệ chốt không được tính, xem CommissionCoverage).</summary>
        public KpiValueDto Commission { get; set; } = new();
        /// <summary>Thực nhận của tiệm = doanh thu thuần − hoa hồng.</summary>
        public KpiValueDto MerchantPayout { get; set; } = new();
        /// <summary>Phí hủy và phí lưu kho/quá hạn đã thu (không tính vào doanh thu tiệm).</summary>
        public KpiValueDto FeesCollected { get; set; } = new();
        /// <summary>Đơn được tạo trong kỳ (theo ngày tạo đơn).</summary>
        public KpiValueDto OrdersCreated { get; set; } = new();
        /// <summary>Đơn hoàn thành trong kỳ (theo thời điểm chuyển sang Completed).</summary>
        public KpiValueDto OrdersCompleted { get; set; } = new();
        /// <summary>Đơn bị hủy trong kỳ (theo thời điểm hủy).</summary>
        public KpiValueDto OrdersCancelled { get; set; } = new();
        /// <summary>Đơn giặt xong đúng hạn: thời điểm chuyển sang Chờ thanh toán ≤ ETA đã chốt; mẫu số = đơn giặt xong trong kỳ có ETA.</summary>
        public RateKpiDto OnTimeRate { get; set; } = new();
        /// <summary>Số đơn có giao dịch tiền giặt trong kỳ nhưng chưa có tỷ lệ hoa hồng chốt (đơn cũ).</summary>
        public int OrdersWithoutCommissionSnapshot { get; set; }
        public CommissionCoverageDto CommissionCoverage { get; set; } = new();
    }

    public class ReportBucketDto
    {
        public int Index { get; set; }
        public string Label { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime PreviousStart { get; set; }
        public DateTime PreviousEnd { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal PreviousNetRevenue { get; set; }
        public int OrdersCreated { get; set; }
        public int OrdersCompleted { get; set; }
        /// <summary>false cho phần sau asOf của kỳ chưa kết thúc (giá trị không phải 0 thật).</summary>
        public bool Computed { get; set; } = true;
    }

    public class TopServiceDto
    {
        public int Rank { get; set; }
        public Guid ServiceId { get; set; }
        /// <summary>Tên/mã/ảnh hiện tại của dịch vụ; giá trị tính theo giá snapshot trên đơn.</summary>
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public string? ThumbnailUrl => ImageUrls.Count > 0 ? ImageUrls[0] : null;
        /// <summary>Số đơn khác nhau có dịch vụ này (đơn có giao dịch tiền giặt trong kỳ).</summary>
        public int OrderCount { get; set; }
        public int PreviousOrderCount { get; set; }
        public decimal? OrderCountChangePercent { get; set; }
        public int Quantity { get; set; }
        /// <summary>Giá trị dịch vụ theo giá snapshot (đơn giá × số lượng), trước phụ phí cân và giảm giá; không phải doanh thu thực nhận.</summary>
        public decimal ListValue { get; set; }
    }

    public class ReportFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = [];
    }
}
