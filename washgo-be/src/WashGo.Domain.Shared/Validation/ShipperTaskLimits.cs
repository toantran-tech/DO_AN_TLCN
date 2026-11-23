namespace WashGo.Domain.Shared.Validation;

public static class ShipperTaskLimits
{
    public const int Minimum = 1;
    public const int Maximum = 20;
    public const int Default = 3;

    public static bool IsValid(int? value) => value is >= Minimum and <= Maximum;
}
