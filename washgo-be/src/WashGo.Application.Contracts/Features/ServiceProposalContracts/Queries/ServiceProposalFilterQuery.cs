using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ServiceProposalContracts.Queries
{
    public class ServiceProposalFilterQuery
    {
        public ServiceApprovalStatus? Status { get; set; }
        public ServiceChangeRequestType? RequestType { get; set; }
        public Guid? MerchantId { get; set; }
        /// <summary>Tìm theo tên dịch vụ đề xuất hoặc mã dịch vụ.</summary>
        public string? Keyword { get; set; }
        public Guid? ServiceId { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
