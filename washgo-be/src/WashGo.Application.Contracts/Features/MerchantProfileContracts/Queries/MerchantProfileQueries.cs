using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts.Queries
{
    public class MerchantProfileRequestFilterQuery
    {
        public ServiceApprovalStatus? Status { get; set; }
        public Guid? MerchantId { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }
}
