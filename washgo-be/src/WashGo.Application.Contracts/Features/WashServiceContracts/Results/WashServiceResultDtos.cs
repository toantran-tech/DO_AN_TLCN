using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Results
{
    public class WashServiceResultDto
    {
        public Guid Id { get; set; }
        public Guid? MerchantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public WashServiceType ServiceType { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int EstimatedDurationHours { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; }
        public bool IsActive { get; set; }
        public long CreatedOn { get; set; }
        /// <summary>Ảnh theo thứ tự lưu (danh mục Merchant); thiếu ảnh trả [].</summary>
        public List<string> ImageUrls { get; set; } = [];
        public string? ThumbnailUrl => ImageUrls.Count > 0 ? ImageUrls[0] : null;
        /// <summary>Phiên bản nội dung (tên, mô tả, giá, thời gian, loại, đơn vị, ảnh); gửi lại làm expectedVersion khi đề xuất sửa.</summary>
        public string? Version { get; set; }
        /// <summary>Đang có đề xuất sửa chờ duyệt (của bất kỳ Merchant nào; không lộ nội dung/người gửi).</summary>
        public bool HasPendingProposal { get; set; }
        /// <summary>Đề xuất chờ duyệt của chính Merchant đang xem (nếu có).</summary>
        public Guid? MyPendingProposalId { get; set; }
        public bool CanProposeUpdate { get; set; }
        /// <summary>PendingProposal, MerchantInactive.</summary>
        public string? ProposeBlockedCode { get; set; }
        public string? ProposeBlockedReason { get; set; }
    }
}

