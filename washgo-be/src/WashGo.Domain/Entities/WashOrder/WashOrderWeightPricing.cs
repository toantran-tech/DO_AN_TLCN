using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder;

public static class WashOrderWeightPricing
{
    public const decimal ExcessPricePerKg = 15000m;

    public static decimal IncludedWeight(LockerBoxSize size) => size switch
    {
        LockerBoxSize.Small => 3m,
        LockerBoxSize.Standard => 5m,
        LockerBoxSize.Large => 10m,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };

    public static (decimal ExcessWeightKg, decimal SurchargeAmount, int ProcessingHours) Calculate(
        decimal actualWeightKg, decimal includedWeightKg, int baseProcessingHours)
    {
        if (actualWeightKg <= 0 || actualWeightKg > 1000 || decimal.Round(actualWeightKg, 2) != actualWeightKg)
            throw new ArgumentOutOfRangeException(nameof(actualWeightKg));
        if (includedWeightKg <= 0 || baseProcessingHours is < 1 or > 8760)
            throw new ArgumentOutOfRangeException(nameof(baseProcessingHours));
        var excess = Math.Max(0m, actualWeightKg - includedWeightKg);
        return (excess, excess * ExcessPricePerKg,
            baseProcessingHours + 2 * (int)decimal.Ceiling(excess));
    }
}
