using System;
using System.Collections.Generic;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results;

public class OrderActualsQuoteDto
{
    public long Revision { get; set; }
    public WashOrderStatus Status { get; set; }
    public decimal PreviousFinalAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightSurchargeAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool PromotionRemoved { get; set; }
    public string? PromotionReasonCode { get; set; }
    public List<OrderActualsLineDto> Items { get; set; } = [];
}

public class OrderActualsLineDto
{
    public Guid OrderItemId { get; set; }
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public int ActualQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
