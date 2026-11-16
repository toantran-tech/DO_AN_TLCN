using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Shared.Enums;

// Fake HTTP only. No merchant keys, database or external requests.
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Payment:Provider"] = "Live", ["Payment:VnPay:TmnCode"] = "test-merchant",
    ["Payment:VnPay:HashSecret"] = "test-secret", ["Payment:VnPay:ReturnUrl"] = "https://example.invalid/return",
    ["Payment:VnPay:PaymentUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    ["Payment:Momo:PartnerCode"] = "test-partner", ["Payment:Momo:AccessKey"] = "test-access",
    ["Payment:Momo:SecretKey"] = "test-secret", ["Payment:Momo:RedirectUrl"] = "https://example.invalid/return",
    ["Payment:Momo:IpnUrl"] = "https://example.invalid/ipn"
}).Build();
var request = new GatewayQueryRequest(PaymentMethod.VnPay, "stable-reference", 10000m,
    new DateTime(2026, 11, 16, 1, 2, 3, DateTimeKind.Utc), "unique-attempt");
var mode = "success";
var calls = 0;
using var handler = new FakeHandler(async http =>
{
    calls++;
    using var payload = JsonDocument.Parse(await http.Content!.ReadAsStringAsync());
    var body = payload.RootElement;
    if (mode == "timeout") throw new TaskCanceledException();
    if (mode == "http-error") return new(HttpStatusCode.ServiceUnavailable);
    if (mode == "broken") return Response("{");
    if (http.RequestUri!.Host == "sandbox.vnpayment.vn")
    {
        Check(body.GetProperty("vnp_TxnRef").GetString() == request.Reference, "VNPay reference changed");
        Check(body.GetProperty("vnp_TransactionDate").GetString() == "20261116080203", "Original VNPay date not preserved");
        var fields = new[] { "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" };
        Check(body.GetProperty("vnp_SecureHash").GetString() == Sign512(string.Join("|", fields.Select(x => body.GetProperty(x).GetString()))), "VNPay query request signature");
        var result = new Dictionary<string, string>
        {
            ["vnp_ResponseId"] = "provider-response", ["vnp_Command"] = "querydr", ["vnp_ResponseCode"] = mode == "notfound" ? "91" : "00",
            ["vnp_Message"] = "Result", ["vnp_TmnCode"] = "test-merchant", ["vnp_TxnRef"] = mode == "wrong-id" ? "another" : request.Reference,
            ["vnp_Amount"] = mode == "wrong-amount" ? "1" : "1000000", ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261116080503",
            ["vnp_TransactionNo"] = "123456", ["vnp_TransactionType"] = mode == "refund" ? "02" : "01",
            ["vnp_TransactionStatus"] = mode == "failure" ? "02" : mode == "pending" ? "01" : mode == "risk" ? "07" : "00",
            ["vnp_OrderInfo"] = "Query", ["vnp_PromotionCode"] = "", ["vnp_PromotionAmount"] = ""
        };
        // Insertion order is the documented response canonical order above.
        result["vnp_SecureHash"] = mode == "bad-signature" ? new string('0', 128) : Sign512(string.Join("|", result.Values));
        return Response(JsonSerializer.Serialize(result));
    }
    Check(http.RequestUri.Host == "test-payment.momo.vn", "Unexpected query endpoint");
    var momo = request with { Method = PaymentMethod.Momo };
    var raw = $"accessKey=test-access&orderId={momo.Reference}&partnerCode=test-partner&requestId={momo.AttemptId}";
    Check(body.GetProperty("signature").GetString() == Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(), "MoMo query request signature");
    return Response(JsonSerializer.Serialize(new
    {
        partnerCode = "test-partner", requestId = mode == "wrong-id" ? "another" : momo.AttemptId,
        orderId = momo.Reference, amount = mode == "wrong-amount" ? 1 : momo.Amount,
        resultCode = mode == "failure" ? 1005 : mode == "pending" ? 1000 : 0, transId = 123456,
        refundTrans = mode == "refund" ? new[] { new { amount = 10000 } } : []
    }));
});
var clients = new FakeClients(handler);
var query = new PaymentQueryGateway(config, clients);
foreach (var method in new[] { PaymentMethod.VnPay, PaymentMethod.Momo })
{
    foreach (var scenario in new[] { "success", "failure", "pending", "wrong-id", "wrong-amount", "refund", "timeout", "broken", "http-error" })
    {
        mode = scenario;
        var result = await query.QueryAsync(request with { Method = method });
        var expected = scenario switch { "success" => GatewayQueryOutcome.Succeeded, "failure" => GatewayQueryOutcome.Failed, "pending" => GatewayQueryOutcome.Pending, _ => GatewayQueryOutcome.Unknown };
        Check(result.Outcome == expected, $"{method}: unsafe {scenario} classification ({result.Outcome})");
    }
}
foreach (var scenario in new[] { "notfound", "bad-signature", "risk" })
{
    mode = scenario;
    Check((await query.QueryAsync(request)).Outcome is GatewayQueryOutcome.Unknown or GatewayQueryOutcome.Pending, "Uncertain VNPay was closed");
}
var before = calls;
Check((await query.QueryAsync(request with { CreatedAt = null })).Outcome == GatewayQueryOutcome.Unknown && before == calls, "Guessed legacy transaction date");
config["Payment:VnPay:QueryEndpoint"] = "https://attacker.invalid/query";
Check((await query.QueryAsync(request)).Outcome == GatewayQueryOutcome.Unknown && before == calls, "Untrusted gateway host called");
Check(!PaymentQueryGateway.AllowedEndpoint(new Uri("http://test-payment.momo.vn/query"), PaymentMethod.Momo), "HTTP accepted");
Check(!PaymentQueryGateway.AllowedEndpoint(new Uri("https://test-payment.momo.vn.attacker.invalid/query"), PaymentMethod.Momo), "Host suffix accepted");
var momoGateway = new MomoGateway(config, NullLogger<MomoGateway>.Instance, clients);
var capabilities = new PaymentCapabilities(config, momoGateway);
foreach (var amount in new[] { 0m, 999m, 1000m, 9000m, 10000m, 50000000m, 50000001m })
{
    var methods = capabilities.For(amount);
    Check(methods.Single(x => x.PaymentMethod == PaymentMethod.VnPay).Enabled == (amount >= 10000), $"VNPay boundary {amount}");
    var momo = methods.Single(x => x.PaymentMethod == PaymentMethod.Momo);
    Check(momo.Enabled == (amount >= 1000 && amount <= 50000000), $"MoMo boundary {amount}");
    Check(momo.WalletOnly == (amount < 10000), "MoMo card minimum not disclosed");
}
Check(!capabilities.For(1000.5m).Single(x => x.PaymentMethod == PaymentMethod.Momo).Enabled, "Fractional VND rounded");
before = calls;
Check((await momoGateway.CreatePayUrlAsync("fractional", 1000.5m, "Test")).Url == null && calls == before, "Invalid amount reached MoMo");
using var walletHandler = new FakeHandler(async http =>
{
    using var payload = JsonDocument.Parse(await http.Content!.ReadAsStringAsync());
    Check(payload.RootElement.GetProperty("requestType").GetString() == "captureWallet", "Low amount offered ATM card");
    Check(payload.RootElement.GetProperty("amount").GetInt64() == 9000, "Gateway amount changed");
    return Response("{\"resultCode\":0,\"payUrl\":\"https://test-payment.momo.vn/checkout\"}");
});
var walletGateway = new MomoGateway(config, NullLogger<MomoGateway>.Instance, new FakeClients(walletHandler));
Check((await walletGateway.CreatePayUrlAsync("wallet-reference", 9000, "Test")).Url != null, "Wallet path unavailable");
config["Payment:Momo:SecretKey"] = null;
Check(!capabilities.For(10000).Single(x => x.PaymentMethod == PaymentMethod.Momo).Enabled, "Missing credentials still advertised live payment");
var now = DateTime.UtcNow;
var tx = new PaymentTransaction { Status = PaymentTransactionStatus.Pending, PaymentMethod = PaymentMethod.VnPay,
    SessionState = PaymentSessionState.Ready, PaymentUrl = "https://sandbox.vnpayment.vn/pay", GatewayExpiresAt = now.AddSeconds(-1) };
var expired = PaymentSessionRules.View(Guid.NewGuid(), PaymentPurpose.Order, [tx], now, true);
Check(!expired.CanContinue && expired.CanVerify && !expired.CanCreateReplacement && PaymentSessionRules.BlocksNewPayment(tx), "Local expiry released session");
var legacy = PaymentSessionRules.View(Guid.NewGuid(), PaymentPurpose.Order, [tx, new PaymentTransaction()], now, true);
Check(legacy.PaymentMethod == null && legacy.SessionId == null && legacy.RequiresReconciliation, "Legacy duplicates arbitrarily selected");
Console.WriteLine("PASS: fake signed gateway queries, identity/amount checks, uncertain outcomes, endpoint restrictions, amount limits, no rounding and expired-session safety.");

static string Sign512(string value) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
static HttpResponseMessage Response(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
sealed class FakeClients(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
