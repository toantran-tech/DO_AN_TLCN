using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class RefundRequestDto
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public Guid? OriginalPaymentTransactionId { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string ResourceUrl => $"/payment/refund-requests/{Id}";
    public string? CustomerEmail { get; set; }
    public string? CustomerPhoneNumber { get; set; }
    public string? CustomerAvatarUrl { get; set; }
    public Guid? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? OriginalAmount { get; set; }
    public decimal? RemainingRefundableAmount { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RefundRequestStatus Status { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RefundReference { get; set; }
    public DateTime? RefundedAt { get; set; }
    public long CreatedOn { get; set; }
}
