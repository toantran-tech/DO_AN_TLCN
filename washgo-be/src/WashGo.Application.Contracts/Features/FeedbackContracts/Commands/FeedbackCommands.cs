using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;
using System.Text.Json.Serialization;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;

namespace WashGo.Application.Contracts.Features.FeedbackContracts.Commands
{
    public class CreateRatingCommand
    {
        public Guid OrderId { get; set; }
        public int? OverallStars { get; set; }
        public int? QualityStars { get; set; }
        public int? SpeedStars { get; set; }
        public int? AttitudeStars { get; set; }
        public int? PackagingStars { get; set; }
        public string? Comment { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        /// <summary>Video MP4/WEBM/MOV đã upload qua /files/videos (tối đa 2).</summary>
        public List<string> VideoUrls { get; set; } = [];
        /// <summary>Mã thao tác do client sinh: gửi lại cùng mã trả kết quả cũ, cùng mã khác nội dung → 409.</summary>
        public Guid? OperationId { get; set; }
    }

    /// <summary>isHidden là trạng thái đích (không phải đảo). Ẩn cần lý do 1-500 ký tự.</summary>
    public class SetRatingVisibilityCommand
    {
        public bool IsHidden { get; set; }
        public string? Reason { get; set; }
        /// <summary>Bắt buộc: version lấy từ GET /rating/{id} hoặc danh sách Admin.</summary>
        public string? ExpectedVersion { get; set; }
        public Guid? OperationId { get; set; }
    }

    /// <summary>Ẩn/hiện hàng loạt 1-50 đánh giá được chọn; action Hide|Show|Publish; Hide cần một lý do chung.</summary>
    public class RatingBatchCommand
    {
        public Guid OperationId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public List<AdministrativeBatchItem> Items { get; set; } = [];
    }

    public class CreateIncidentCommand
    {
        [JsonRequired]
        public decimal ItemValue { get; set; }
        public Guid OrderId { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        /// <summary>Video MP4/WEBM/MOV đã upload qua /files/videos (tối đa 2).</summary>
        public List<string> VideoUrls { get; set; } = [];
        /// <summary>Mã do client sinh (tối đa 64 ký tự); gửi lại cùng mã trả về khiếu nại đã tạo, không tạo trùng.</summary>
        public string? ClientRequestId { get; set; }
    }

    /// <summary>Khách sửa khiếu nại khi còn chờ xử lý; thay toàn bộ mô tả và minh chứng.</summary>
    public class UpdateIncidentCommand
    {
        public decimal? ItemValue { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
    }

    public class WithdrawIncidentCommand
    {
        public string? Reason { get; set; }
    }

    /// <summary>Tin nhắn trao đổi trong khiếu nại, có thể kèm minh chứng bổ sung.</summary>
    public class AddIncidentMessageCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
    }

    public class ResolveIncidentCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string? ExpectedSettingsVersion { get; set; }
        public string ResolutionNote { get; set; } = string.Empty;
        public ResponsibleParty? ResponsibleParty { get; set; }
        public CompensationLevel CompensationLevel { get; set; }
        /// <summary>Giá trị đồ bị ảnh hưởng; tiền bồi thường = giá trị x % theo mức.</summary>
        public decimal ItemValue { get; set; }
    }

    public class RejectIncidentCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ConfirmCompensationCommand
    {
        public List<string> ProofImageUrls { get; set; } = [];
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string Reference { get; set; } = string.Empty;
    }
}
