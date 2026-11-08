using System;
using System.Collections.Generic;
using System.Text.Json;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AuditContracts.Results
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        /// <summary>Thời điểm (UTC, hậu tố Z).</summary>
        public DateTime OccurredAt { get; set; }
        public Guid? ActorId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string ActorRole { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        public string? ResourceCode { get; set; }
        public AuditResult Result { get; set; }
        public string? Reason { get; set; }
        public string? ErrorCode { get; set; }
        /// <summary>Tên các trường đã thay đổi (không kèm giá trị nhạy cảm).</summary>
        public List<string> ChangedFields { get; set; } = [];
        /// <summary>Chi tiết đã lọc dữ liệu nhạy cảm (mật khẩu, OTP, token, mã mở tủ, giấy tờ tùy thân không bao giờ có ở đây).</summary>
        public JsonElement? Details { get; set; }
        public string? CorrelationId { get; set; }
    }
}
