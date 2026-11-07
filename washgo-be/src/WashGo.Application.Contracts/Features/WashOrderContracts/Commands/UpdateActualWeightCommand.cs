namespace WashGo.Application.Contracts.Features.WashOrderContracts.Commands;

public class UpdateActualWeightCommand : GuardedOrderCommand
{
    public decimal WeightKg { get; set; }
}
