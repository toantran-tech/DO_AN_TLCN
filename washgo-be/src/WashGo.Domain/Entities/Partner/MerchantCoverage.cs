using System;

namespace WashGo.Domain.Entities.Partner;

/// <summary>
/// Service radius is a hard rule: a Merchant may serve a locker only when the straight-line (Haversine) distance
/// from its store to the locker is at most its own ServiceRadiusKm. Compare unrounded values; round for display only.
/// </summary>
public static class MerchantCoverage
{
    public const double EarthRadiusKm = 6371;
    public const string OutsideRadius = "MerchantOutsideServiceRadius";
    public const string MerchantLocationUnavailable = "MerchantLocationUnavailable";
    public const string LockerLocationRequired = "LockerLocationRequired";
    public const string NoEligibleMerchant = "NoEligibleMerchantForLocker";
    public const string WouldExcludeLockers = "MerchantCoverageWouldExcludeLockers";

    public static bool HasLocation(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && !(latitude == 0 && longitude == 0);

    public static bool HasLocation(decimal latitude, decimal longitude) => HasLocation((double)latitude, (double)longitude);

    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double rad = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * rad / 2), 2) +
                Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Pow(Math.Sin((lng2 - lng1) * rad / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1, a)));
    }

    /// <summary>Null when either side has no usable coordinates (never treated as distance 0).</summary>
    public static double? DistanceKm(decimal merchantLatitude, decimal merchantLongitude, double lockerLatitude, double lockerLongitude) =>
        HasLocation(merchantLatitude, merchantLongitude) && HasLocation(lockerLatitude, lockerLongitude)
            ? DistanceKm((double)merchantLatitude, (double)merchantLongitude, lockerLatitude, lockerLongitude)
            : null;

    public static double? DistanceKm(Merchant merchant, double lockerLatitude, double lockerLongitude) =>
        DistanceKm(merchant.Latitude, merchant.Longitude, lockerLatitude, lockerLongitude);

    public static bool Covers(decimal merchantLatitude, decimal merchantLongitude, int serviceRadiusKm, double lockerLatitude, double lockerLongitude) =>
        DistanceKm(merchantLatitude, merchantLongitude, lockerLatitude, lockerLongitude) is { } distance && distance <= serviceRadiusKm;

    public static bool Covers(Merchant merchant, double lockerLatitude, double lockerLongitude) =>
        Covers(merchant.Latitude, merchant.Longitude, merchant.ServiceRadiusKm, lockerLatitude, lockerLongitude);

    /// <summary>Why a Merchant cannot serve the locker position, or null when it can (location rules only).</summary>
    public static string? LocationProblem(Merchant merchant, double lockerLatitude, double lockerLongitude) =>
        !HasLocation(lockerLatitude, lockerLongitude) ? LockerLocationRequired
        : !HasLocation(merchant.Latitude, merchant.Longitude) ? MerchantLocationUnavailable
        : Covers(merchant, lockerLatitude, lockerLongitude) ? null : OutsideRadius;

    public static double Display(double distanceKm) => Math.Round(distanceKm, 2);
}
