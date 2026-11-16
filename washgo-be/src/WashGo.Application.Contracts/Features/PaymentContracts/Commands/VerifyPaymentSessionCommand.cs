using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.PaymentContracts.Commands;

public class VerifyPaymentSessionCommand
{
    public Guid OperationId { get; set; }
    public PaymentPurpose Purpose { get; set; }
}
