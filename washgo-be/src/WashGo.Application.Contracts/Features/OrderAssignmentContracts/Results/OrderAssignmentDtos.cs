using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;

namespace WashGo.Application.Contracts.Features.OrderAssignmentContracts.Results
{
    /// <summary>UC 3.3.1: đơn đã gửi đồ, chưa có Merchant.</summary>
    public class WaitingOrderDto
    {
        public Guid Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid LockerId { get; set; }
        public string LockerName { get; set; } = string.Empty;
        public string BoxNumber { get; set; } = string.Empty;
        public LockerBoxSize? BoxSize { get; set; }
        public List<string> Services { get; set; } = [];
        /// <summary>Dịch vụ của đơn kèm ảnh đại diện hiện tại của dịch vụ.</summary>
        public List<OrderServiceRefDto> ServiceItems { get; set; } = [];
        /// <summary>Ảnh hiện tại của trạm (không phải ảnh chụp lúc đặt đơn); thiếu ảnh = [] / null.</summary>
        public List<string> LockerImageUrls { get; set; } = [];
        public string? LockerThumbnailUrl { get; set; }
        public DateTime? DepositedAt { get; set; }
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public Guid? ServiceAreaId { get; set; }
        public string? ServiceAreaName { get; set; }
        /// <summary>Đã chờ phân công quá hạn (highlight).</summary>
        public bool IsOverdue { get; set; }
        public int WaitingMinutes { get; set; }
    }

    /// <summary>Merchant phụ trách locker của đơn, kèm số liệu để Admin chọn.</summary>
    public class MerchantCandidateDto
    {
        public Guid MerchantId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        /// <summary>Điểm đánh giá công khai; null khi chưa có đánh giá công khai (không trả 5 sao mặc định).</summary>
        public decimal? Rating { get; set; }
        public decimal RatingForScore { get; set; }
        public double PriorityScore { get; set; }
        public double LoadScore { get; set; }
        public double RatingScore { get; set; }
        public double DistanceScore { get; set; }
        public string Algorithm => "WeightedScore";
        /// <summary>Số đánh giá công khai (không ẩn); 0 = Rating là giá trị mặc định, chưa có khách đánh giá.</summary>
        public int PublicRatingCount { get; set; }
        public bool HasPublicRating => PublicRatingCount > 0;
        public decimal CommissionRate { get; set; }
        public int CurrentLoad { get; set; }
        public int MaxCapacity { get; set; }
        public double? DistanceKm { get; set; }
        /// <summary>Độ ưu tiên của Merchant tại locker (1 cao nhất).</summary>
        public int Priority { get; set; }
        /// <summary>Điểm gợi ý 0–100: ưu tiên tại locker, tải lượng còn trống, đánh giá, khoảng cách.</summary>
        public double Score { get; set; }
    }

    public class OrderCandidatesDto
    {
        public WaitingOrderDto Order { get; set; } = new();
        public List<MerchantCandidateDto> Merchants { get; set; } = [];
    }

    public class AutoSuggestionDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public MerchantCandidateDto? Suggested { get; set; }
        public string? Message { get; set; }
    }

    public class AssignmentResultDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public decimal? CommissionRateApplied { get; set; }
        public long ResultRevision { get; set; }
    }

    /// <summary>Kết quả một thao tác phân công Merchant (đơn lẻ hoặc lô) tra theo operationId sau timeout.</summary>
    public class AssignmentOperationDto
    {
        public Guid OperationId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<AssignmentResultDto> Results { get; set; } = [];
    }
}
