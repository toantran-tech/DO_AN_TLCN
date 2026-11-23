using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WashGo.Application.Features.PaymentApplication.Services;

// No external gateway or credentials. Exercise request identity and uncertain
// outcomes without pretending that a timeout proves no payment was created.
var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Payment:Momo:PartnerCode"] = "test-partner", ["Payment:Momo:AccessKey"] = "test-access",
    ["Payment:Momo:SecretKey"] = "test-only", ["Payment:Momo:RedirectUrl"] = "https://example.invalid/return",
    ["Payment:Momo:IpnUrl"] = "https://example.invalid/ipn", ["Payment:Momo:Endpoint"] = "https://example.invalid/create"
}).Build();
var references = new List<string>();
var mode = "ok";
using var handler = new GatewayHandler(async request =>
{
    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
    var reference = body.RootElement.GetProperty("orderId").GetString()!;
    if (reference != body.RootElement.GetProperty("requestId").GetString()) throw new Exception("Request identity changed.");
    references.Add(reference);
    if (mode == "timeout") throw new TaskCanceledException("Simulated lost response after provider accepted request.");
    if (mode == "network") throw new HttpRequestException("Simulated connection loss.");
    var json = mode switch
    {
        "reject" => "{\"resultCode\":1002,\"message\":\"Rejected\"}",
        "pending" => "{\"resultCode\":1000}", "processing" => "{\"resultCode\":7002}",
        "broken" => "invalid-json", "empty" => "null", "missing-url" => "{\"resultCode\":0}",
        _ => "{\"resultCode\":0,\"payUrl\":\"https://example.invalid/checkout\"}"
    };
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
});
var gateway = new MomoGateway(settings, NullLogger<MomoGateway>.Instance, new GatewayClients(handler));
var first = await gateway.CreatePayUrlAsync("stable-reference", 100000, "Test order");
if (first.Url == null || first.IsUncertain) throw new Exception("Valid gateway URL rejected.");
foreach (var scenario in new[] { "timeout", "network", "broken", "empty", "missing-url", "pending", "processing", "reject" })
{
    mode = scenario;
    var before = references.Count;
    var result = await gateway.CreatePayUrlAsync("stable-reference", 100000, "Test order");
    if (result.Url != null || result.IsUncertain != (scenario != "reject") || references.Count != before + 1)
        throw new Exception($"Gateway outcome/retry mismatch: {scenario}");
}
if (references.Any(x => x != "stable-reference")) throw new Exception("Gateway generated a different reference.");
if (PaymentGatewayStatusRules.IsVnPayFinalFailure("07", "07") ||
    PaymentGatewayStatusRules.IsVnPayFinalFailure("99", "04") ||
    !PaymentGatewayStatusRules.IsVnPayFinalFailure("24", "02")) throw new Exception("VNPay pending/risk classification mismatch.");
// Result pages for both gateways come from verified server state only.
static void Expect(bool ok, string message) { if (!ok) throw new Exception(message); }
WashGo.Domain.Entities.Payment.PaymentTransaction Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus status, string? reason = null, bool closed = true) =>
    new() { Status = status, FailureReason = reason, VerifiedClosedAt = closed ? DateTime.UtcNow : null, Purpose = WashGo.Domain.Shared.Enums.PaymentPurpose.Order };
var paid = new WashGo.Domain.Entities.WashOrder.WashOrder { PaymentStatus = WashGo.Domain.Shared.Enums.PaymentStatus.Paid };
var unpaid = new WashGo.Domain.Entities.WashOrder.WashOrder { PaymentStatus = WashGo.Domain.Shared.Enums.PaymentStatus.Unpaid };
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Succeeded), paid).Page == "success", "Verified + applied = success");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Succeeded), unpaid).Page == "review", "Success not applied to the order = review");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Pending, closed: false), unpaid).Page == "pending", "Pending stays pending");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "VNPay response=24, status=02"), unpaid).Page == "cancelled", "VNPay 24 = customer cancelled");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "MoMo resultCode=1006: denied"), unpaid).Page == "cancelled", "MoMo 1006 = customer cancelled");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "MoMo resultCode=1006: verified by gateway query"), unpaid).Page == "cancelled", "Query-verified MoMo cancel");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "VNPay response=51, status=02"), unpaid).Page == "failed", "VNPay 51 = failed");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "MoMo resultCode=1004: limit"), unpaid).Page == "failed", "MoMo 1004 = failed");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Failed, "VNPay response=240"), unpaid).Page == "failed", "Prefix match only on the whole code");
Expect(PaymentOutcomeRules.Classify(Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.RequiresReview), paid).Page == "review", "Review status");
var unapplied = Tx(WashGo.Domain.Shared.Enums.PaymentTransactionStatus.Succeeded); unapplied.RequiresReconciliation = true;
Expect(PaymentOutcomeRules.Classify(unapplied, paid).Page == "review", "Unapplied receipt = review, never success");
Console.WriteLine("PASS: stable MoMo orderId/requestId, no hidden HTTP retries, known rejection versus uncertain timeout/network/JSON/missing URL; result pages from verified state (success/failed/cancelled/pending/review) for both gateways.");

sealed class GatewayHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}

sealed class GatewayClients(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name != MomoGateway.HttpClientName) throw new Exception("Wrong HTTP client registration.");
        return new HttpClient(handler, disposeHandler: false);
    }
}
