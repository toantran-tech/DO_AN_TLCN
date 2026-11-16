namespace WashGo.Application.Features.PaymentApplication.Services;

public interface IPaymentVerificationRunner
{
    Task DrainVerificationAsync();
}
