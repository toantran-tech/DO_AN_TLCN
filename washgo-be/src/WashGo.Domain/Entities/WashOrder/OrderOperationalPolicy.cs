using System.Text.Json;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Domain.Entities.WashOrder;

/// <summary>Order-specific operational terms. Legacy capture is a declared baseline, not a claim about historic terms.</summary>
public static class OrderOperationalPolicy
{
    public static void Capture(WashOrder order, SystemSetting settings, string basis = "order-created")
    {
        order.OperationalSettingsJson = JsonSerializer.Serialize(settings);
        order.OperationalPolicyCapturedAt = DateTime.UtcNow;
        order.OperationalPolicyBasis = basis;
        order.CollectSlaHoursSnapshot = settings.MerchantCollectSlaHours;
        order.ReturnSlaHoursSnapshot = settings.MerchantReturnSlaHours;
    }
    public static SystemSetting Resolve(WashOrder order, SystemSetting fallback) =>
        string.IsNullOrWhiteSpace(order.OperationalSettingsJson) ? fallback :
        JsonSerializer.Deserialize<SystemSetting>(order.OperationalSettingsJson) ?? throw new InvalidOperationException("Invalid stored order policy.");
}
