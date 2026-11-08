using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.AuditContracts.Queries
{
    /// <summary>
    /// Lọc nhật ký hoạt động. fromDate/toDate: Unix giây (UTC, gồm cả hai đầu, 0 = không giới hạn).
    /// keyword tìm trong tên người thao tác, mã đối tượng, lý do. Mới nhất trước; page bắt đầu từ 1, take ≤ 100.
    /// </summary>
    public class AuditLogFilterQuery : BaseFilterQuery
    {
        public Guid? ActorId { get; set; }
        /// <summary>Admin, Merchant, Shipper, Customer, Anonymous, System.</summary>
        public string? ActorRole { get; set; }
        /// <summary>Auth, Merchant, MerchantProfile, ServiceProposal, Shipper, Order, Refund, Finance, SystemSetting.</summary>
        public string? Module { get; set; }
        public string? Action { get; set; }
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public AuditResult? Result { get; set; }
    }

    /// <summary>Hoạt động của chính tài khoản đang đăng nhập (phân trang, mới nhất trước).</summary>
    public class MyActivityQuery
    {
        public int? Page { get; set; }
        public int? Take { get; set; }
        public string? Module { get; set; }
    }
}
