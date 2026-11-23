using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services;

/// <summary>Server-to-server query only. Never accepts gateway URLs or results from the browser.</summary>
public class PaymentQueryGateway(IConfiguration config, IHttpClientFactory clients) : ITransientDependency
{
    public const string ClientName = "WashGoPaymentQuery";
    public async Task<GatewayQueryResult> QueryAsync(GatewayQueryRequest request)
    {
        try
        {
            return request.Method == PaymentMethod.VnPay ? await VnPayAsync(request) :
                request.Method == PaymentMethod.Momo ? await MomoAsync(request) : new(GatewayQueryOutcome.Unknown, "UnsupportedGateway");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return new(GatewayQueryOutcome.Unknown, "GatewayQueryUnverified");
        }
    }

    public static bool AllowedEndpoint(Uri uri, PaymentMethod method) => uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && uri.IsDefaultPort &&
        (method == PaymentMethod.VnPay
            ? uri.Host is "sandbox.vnpayment.vn" or "pay.vnpay.vn"
            : uri.Host is "test-payment.momo.vn" or "payment.momo.vn");

    private Uri? Endpoint(PaymentMethod method)
    {
        var vn = method == PaymentMethod.VnPay;
        var value = config[$"Payment:{(vn ? "VnPay" : "Momo")}:QueryEndpoint"];
        if (string.IsNullOrWhiteSpace(value))
        {
            var create = config[vn ? "Payment:VnPay:PaymentUrl" : "Payment:Momo:Endpoint"];
            if (Uri.TryCreate(create, UriKind.Absolute, out var existing))
            {
                if (vn && existing.Host == "sandbox.vnpayment.vn") value = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
                if (!vn && existing.Host == "test-payment.momo.vn") value = "https://test-payment.momo.vn/v2/gateway/api/query";
            }
            else if (!vn) value = "https://test-payment.momo.vn/v2/gateway/api/query";
        }
        return Uri.TryCreate(value, UriKind.Absolute, out var endpoint) && AllowedEndpoint(endpoint, method) ? endpoint : null;
    }

    private async Task<GatewayQueryResult> VnPayAsync(GatewayQueryRequest request)
    {
        var secret = config["Payment:VnPay:HashSecret"];
        var merchant = config["Payment:VnPay:TmnCode"];
        var endpoint = Endpoint(PaymentMethod.VnPay);
        if (endpoint == null || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(merchant))
            return new(GatewayQueryOutcome.Unknown, "GatewayQueryNotConfigured");
        if (!request.CreatedAt.HasValue) return new(GatewayQueryOutcome.Unknown, "LegacyGatewayDateUnknown");
        var payload = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = request.AttemptId, ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = merchant, ["vnp_TxnRef"] = request.Reference,
            ["vnp_TransactionDate"] = VnDate(request.CreatedAt.Value), ["vnp_CreateDate"] = VnDate(DateTime.UtcNow),
            ["vnp_IpAddr"] = config["Payment:VnPay:QueryIpAddress"] ?? "127.0.0.1",
            ["vnp_OrderInfo"] = $"Truy van GD {request.Reference}"
        };
        var fields = new[] { "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" };
        payload["vnp_SecureHash"] = Hash512(secret, string.Join("|", fields.Select(x => payload[x])));
        using var http = clients.CreateClient(ClientName);
        using var response = await http.PostAsJsonAsync(endpoint, payload);
        if (!response.IsSuccessStatusCode) return new(GatewayQueryOutcome.Unknown, "GatewayQueryHttpError");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ValidateVnPay(body.RootElement, request, merchant, secret);
    }

    public static GatewayQueryResult ValidateVnPay(JsonElement root, GatewayQueryRequest request, string merchant, string secret)
    {
        var fields = new[] { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode", "vnp_PromotionAmount" };
        var expected = Hash512(secret, string.Join("|", fields.Select(x => Text(root, x))));
        if (!SecureEquals(expected, Text(root, "vnp_SecureHash")) || Text(root, "vnp_Command") != "querydr" ||
            Text(root, "vnp_TmnCode") != merchant || Text(root, "vnp_TxnRef") != request.Reference ||
            !decimal.TryParse(Text(root, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var cents) || cents != request.Amount * 100m ||
            Text(root, "vnp_ResponseCode") != "00" || Text(root, "vnp_TransactionType") != "01")
            return new(GatewayQueryOutcome.Unknown, "GatewayQueryUnverified");
        var status = Text(root, "vnp_TransactionStatus");
        var id = Text(root, "vnp_TransactionNo");
        if (status == "00" && long.TryParse(id, out var transactionId) && transactionId > 0)
            return new(GatewayQueryOutcome.Succeeded, "GatewayVerifiedSuccess", id);
        if (status == "02") return new(GatewayQueryOutcome.Failed, "GatewayVerifiedFailure", id);
        return new(GatewayQueryOutcome.Pending, "GatewayPendingOrReview", id);
    }

    private async Task<GatewayQueryResult> MomoAsync(GatewayQueryRequest request)
    {
        var secret = config["Payment:Momo:SecretKey"]; var access = config["Payment:Momo:AccessKey"]; var merchant = config["Payment:Momo:PartnerCode"];
        var endpoint = Endpoint(PaymentMethod.Momo);
        if (endpoint == null || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(merchant))
            return new(GatewayQueryOutcome.Unknown, "GatewayQueryNotConfigured");
        if (decimal.Truncate(request.Amount) != request.Amount) return new(GatewayQueryOutcome.Unknown, "LegacyQueryAmountUnsupported");
        var raw = $"accessKey={access}&orderId={request.Reference}&partnerCode={merchant}&requestId={request.AttemptId}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        using var http = clients.CreateClient(ClientName);
        using var response = await http.PostAsJsonAsync(endpoint, new { partnerCode = merchant, requestId = request.AttemptId, orderId = request.Reference, lang = "vi", signature });
        if (!response.IsSuccessStatusCode) return new(GatewayQueryOutcome.Unknown, "GatewayQueryHttpError");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ValidateMomo(body.RootElement, request, merchant);
    }

    // Query v2 documents a signed REQUEST, not an IPN-shaped response signature.
    // Caller enforces HTTPS, official endpoint allowlist, no redirects and request correlation.
    public static GatewayQueryResult ValidateMomo(JsonElement root, GatewayQueryRequest request, string merchant)
    {
        if (Text(root, "partnerCode") != merchant || Text(root, "requestId") != request.AttemptId || Text(root, "orderId") != request.Reference ||
            !decimal.TryParse(Text(root, "amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount != request.Amount ||
            !int.TryParse(Text(root, "resultCode"), out var code) ||
            root.TryGetProperty("refundTrans", out var refunds) && refunds.ValueKind == JsonValueKind.Array && refunds.GetArrayLength() > 0)
            return new(GatewayQueryOutcome.Unknown, "GatewayQueryUnverified");
        var id = Text(root, "transId");
        if (code == 0 && long.TryParse(id, out var transactionId) && transactionId > 0)
            return new(GatewayQueryOutcome.Succeeded, "GatewayVerifiedSuccess", id);
        if (PaymentGatewayStatusRules.IsMomoFinalFailure(code)) return new(GatewayQueryOutcome.Failed, "GatewayVerifiedFailure", id, $"MoMo resultCode={code}");
        return new(GatewayQueryOutcome.Pending, "GatewayPendingOrReview", id);
    }

    private static string VnDate(DateTime utc) => new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    private static string Text(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "" : "";
    private static string Hash512(string secret, string raw) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    private static bool SecureEquals(string expected, string supplied)
    {
        if (supplied.Length != expected.Length) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(supplied)); }
        catch (FormatException) { return false; }
    }
}
