using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public enum GatewayQueryOutcome { Unknown, Pending, Succeeded, Failed }

public sealed record GatewayQueryResult(GatewayQueryOutcome Outcome, string ReasonCode, string? ProviderTransactionId = null);

public sealed record GatewayQueryRequest(PaymentMethod Method, string Reference, decimal Amount,
    DateTime? CreatedAt, string AttemptId);
