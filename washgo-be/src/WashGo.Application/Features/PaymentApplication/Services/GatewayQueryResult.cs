using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

public enum GatewayQueryOutcome { Unknown, Pending, Succeeded, Failed }

/// <summary>GatewayCode keeps the provider result code of a verified failure (e.g. MoMo 1006 = customer cancelled).</summary>
public sealed record GatewayQueryResult(GatewayQueryOutcome Outcome, string ReasonCode, string? ProviderTransactionId = null, string? GatewayCode = null);

public sealed record GatewayQueryRequest(PaymentMethod Method, string Reference, decimal Amount,
    DateTime? CreatedAt, string AttemptId);
