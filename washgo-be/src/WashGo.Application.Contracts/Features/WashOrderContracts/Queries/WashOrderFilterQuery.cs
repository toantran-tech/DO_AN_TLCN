using System;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Queries
{
    public class WashOrderFilterQuery : BaseFilterQuery
    {
        public WashOrderStatus? Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public Guid? LockerId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ShipperId { get; set; }
        public Guid? MerchantId { get; set; }
    }
}
