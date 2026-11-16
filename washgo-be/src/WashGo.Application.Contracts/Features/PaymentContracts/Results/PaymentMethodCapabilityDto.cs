using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class PaymentMethodCapabilityDto
{
    public PaymentMethod PaymentMethod { get; set; }
    public bool Enabled { get; set; }
    public decimal PolicyMinAmount { get; set; }
    public decimal? GatewayMinAmount { get; set; }
    public decimal? GatewayMaxAmount { get; set; }
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string LimitSource { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public bool WalletOnly { get; set; }
}
