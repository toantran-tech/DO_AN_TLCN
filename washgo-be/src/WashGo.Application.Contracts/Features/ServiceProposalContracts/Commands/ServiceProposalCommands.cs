using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ServiceProposalContracts.Commands
{
    /// <summary>Nội dung Merchant đề xuất (thêm mới hoặc chỉnh sửa dịch vụ), cũng là dữ liệu lưu trong đề xuất.</summary>
    public class ServiceProposalCommand
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>Giá cơ bản (gói theo kg: giá gói; theo chiếc/đôi: đơn giá).</summary>
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public int EstimatedDurationHours { get; set; } = 24;
        /// <summary>Danh mục dịch vụ.</summary>
        public WashServiceType ServiceType { get; set; } = WashServiceType.StandardWash;
        public string Unit { get; set; } = "kg";
        /// <summary>
        /// Ảnh dịch vụ: null = giữ ảnh hiện tại (khi đề xuất sửa), [] = xóa hết, danh sách = bộ ảnh mới theo thứ tự.
        /// Ảnh đang thuộc dịch vụ được giữ lại; ảnh mới phải do chính Merchant upload.
        /// </summary>
        public List<string>? ImageUrls { get; set; }
        public string? Reason { get; set; }
        /// <summary>Mã yêu cầu FE sinh một lần (hoặc header Idempotency-Key): gửi lại trả đề xuất đã tạo, không báo Admin lần nữa.</summary>
        public Guid? ClientRequestId { get; set; }
        /// <summary>`version` của dịch vụ lúc Merchant xem (đề xuất sửa); khác bản hiện tại → 409 StaleVersion.</summary>
        public string? ExpectedVersion { get; set; }
    }

    public class RejectServiceProposalCommand
    {
        public string Reason { get; set; } = string.Empty;
    }
}
