namespace WashGo.Application.Contracts.Features.LoyaltyContracts.Results;

public class LoyaltyBalanceDto
{
    public long TotalPoints { get; set; }
    public long HeldPoints { get; set; }
    public long AvailablePoints { get; set; }
    public long DebtPoints { get; set; }
    public int VndPerPoint => 1;
    public decimal EarnPercent { get; set; }
    public decimal MaxUsePercent { get; set; }
}
