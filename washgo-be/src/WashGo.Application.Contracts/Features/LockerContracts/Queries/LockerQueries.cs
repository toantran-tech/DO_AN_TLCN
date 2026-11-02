using System;
using System.Collections.Generic;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Application.Services;

namespace WashGo.Application.Contracts.Features.LockerContracts.Queries
{
    public class LockerFilterQuery : BaseFilterQuery
    {
        public LockerStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public Guid? ServiceAreaId { get; set; }
        public Guid? MerchantId { get; set; }
    }

    /// <summary>Lọc lịch sử theo thời gian đặt đồ vào ô (Unix giây).</summary>
    public class LockerUsageQuery
    {
        public long? FromDate { get; set; }
        public long? ToDate { get; set; }
        /// <summary>Chỉ lấy lịch sử của một ô (UC 3.4.5).</summary>
        public Guid? BoxId { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}

