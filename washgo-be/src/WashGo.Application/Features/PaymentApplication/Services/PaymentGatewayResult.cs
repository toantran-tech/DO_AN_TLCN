namespace WashGo.Application.Features.PaymentApplication.Services;

public sealed record PaymentGatewayResult(string? Url, string? Error, bool IsUncertain = false);
