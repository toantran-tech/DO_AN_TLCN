using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Commands;

public class ConfirmOrderActualsCommand : GuardedOrderCommand
{
    public decimal? ActualWeightKg { get; set; }
    public List<OrderActualQuantityCommand> Items { get; set; } = [];
    public string? Note { get; set; }
    public decimal? ExpectedFinalAmount { get; set; }
}

public class OrderActualQuantityCommand
{
    public Guid OrderItemId { get; set; }
    public int? ActualQuantity { get; set; }
}
