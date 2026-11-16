using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Contracts.Features.PaymentContracts.Results;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public class PaymentCapabilities(IConfiguration config, MomoGateway momo) : ITransientDependency
{
    public bool Live => !string.IsNullOrWhiteSpace(config["Payment:Provider"]) &&
        !string.Equals(config["Payment:Provider"], "Mock", StringComparison.OrdinalIgnoreCase);

    public List<PaymentMethodCapabilityDto> For(decimal amount) => [For(PaymentMethod.VnPay, amount), For(PaymentMethod.Momo, amount)];

    private PaymentMethodCapabilityDto For(PaymentMethod method, decimal amount)
    {
        var vn = method == PaymentMethod.VnPay;
        var key = $"Payment:Limits:{(vn ? "VnPay" : "Momo")}";
        var policyMin = config.GetValue<decimal?>($"{key}:PolicyMinAmount") ?? (vn ? 10000m : 1000m);
        var gatewayMin = config.GetValue<decimal?>($"{key}:GatewayMinAmount") ?? (vn ? (decimal?)null : 1000m);
        var gatewayMax = config.GetValue<decimal?>($"{key}:GatewayMaxAmount") ?? (vn ? (decimal?)null : 50000000m);
        var dto = new PaymentMethodCapabilityDto
        {
            PaymentMethod = method, PolicyMinAmount = policyMin, GatewayMinAmount = gatewayMin,
            GatewayMaxAmount = gatewayMax, MinAmount = Math.Max(policyMin, gatewayMin ?? 0), MaxAmount = gatewayMax,
            LimitSource = vn ? "WashGoPolicyAndMerchantConfiguration" : "MoMoWalletDefaultsAndMerchantConfiguration"
        };
        var type = config["Payment:Momo:RequestType"] ?? "payWithMethod";
        var configured = vn
            ? new[] { "TmnCode", "HashSecret", "ReturnUrl" }.All(x => !string.IsNullOrWhiteSpace(config[$"Payment:VnPay:{x}"]))
            : momo.IsConfigured;
        dto.ReasonCode = policyMin < 0 || gatewayMin < 0 || gatewayMax <= 0 || gatewayMax < dto.MinAmount ? "PaymentLimitsMisconfigured" :
            !config.GetValue($"{key}:Enabled", true) ? "PaymentMethodDisabled" :
            Live && !configured ? "GatewayNotConfigured" :
            !vn && type is not ("payWithMethod" or "captureWallet") ? "MomoRequestTypeNotSupported" :
            amount <= 0 ? "NothingToPay" : amount < dto.MinAmount ? "BelowMinimumAmount" :
            gatewayMax.HasValue && amount > gatewayMax.Value ? "AboveMaximumAmount" :
            !vn && decimal.Truncate(amount) != amount ? "WholeVndRequired" : null;
        dto.Enabled = dto.ReasonCode == null;
        dto.WalletOnly = !vn && amount < 10000m;
        return dto;
    }
}
