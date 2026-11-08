namespace WashGo.Application.Contracts.Features.MerchantContracts.Results;
using System;

public class MerchantSummaryDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
    public int Suspended { get; set; }
    public int PendingProfileRequests { get; set; }
    public DateTime GeneratedAt { get; set; }
}
