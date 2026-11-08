using System;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Commands
{
    public class CreateSettlementCommand
    {
        public Guid? OperationId { get; set; }
        public string? QuoteToken { get; set; }
        public Guid MerchantId { get; set; }
        /// <summary>Unix giây, bắt buộc.</summary>
        public long FromDate { get; set; }
        public long ToDate { get; set; }
    }

    public class ConfirmSettlementPaidCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public string Reference { get; set; } = string.Empty;
    }
}
