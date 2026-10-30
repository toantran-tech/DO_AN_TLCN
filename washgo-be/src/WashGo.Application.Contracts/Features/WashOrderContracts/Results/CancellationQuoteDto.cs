using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    public class CancellationQuoteDto
    {
        public Guid OrderId { get; set; }
        public bool CanCancel { get; set; }
        public string? Reason { get; set; }
        public WashOrderStatus Status { get; set; }
        public decimal BasisAmount { get; set; }
        public int FeePercent { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal FeeOutstanding { get; set; }
    }
}
