using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.LockerContracts.Results;

/// <summary>A Merchant judged against a locker position (GET /locker/eligible-merchants).</summary>
public class EligibleMerchantDto
{
    public Guid MerchantId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public MerchantStatus Status { get; set; }
    public int ServiceRadiusKm { get; set; }
    /// <summary>Store → locker straight-line distance in km, rounded to 2 decimals for display; null when a position is missing.</summary>
    public double? DistanceKm { get; set; }
    /// <summary>Decided on the unrounded distance.</summary>
    public bool WithinServiceRadius { get; set; }
    public bool Eligible { get; set; }
    /// <summary>MerchantOutsideServiceRadius, MerchantLocationUnavailable, LockerLocationRequired, MerchantInactive, AccountInactive, ContractExpired or MerchantNotFound; null when eligible.</summary>
    public string? BlockedReasonCode { get; set; }
    public bool IsLinked { get; set; }
    public int? LinkPriority { get; set; }
}

/// <summary>A stored Merchant–locker link that no longer satisfies the service radius (GET /locker/coverage-issues).</summary>
public class LockerCoverageIssueDto
{
    public Guid LockerId { get; set; }
    public string LockerCode { get; set; } = string.Empty;
    public string LockerName { get; set; } = string.Empty;
    public LockerStatus LockerStatus { get; set; }
    public Guid MerchantId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public double? DistanceKm { get; set; }
    public int ServiceRadiusKm { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    /// <summary>Another active Merchant still covers the locker, so customers can keep using it.</summary>
    public bool LockerStillServed { get; set; }
    /// <summary>Smallest whole radius (km) that would cover the locker; null when above the 100 km limit or unknown.</summary>
    public int? SuggestedServiceRadiusKm { get; set; }
}

/// <summary>A locker a Merchant location/radius change would leave outside the service radius.</summary>
public class CoverageExcludedLockerDto
{
    public Guid LockerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? DistanceKm { get; set; }
    public int ServiceRadiusKm { get; set; }
}
