using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using  System.Text.Json;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    /// <summary>
    /// MoMo "All-in-one" v2 gateway (UC 1.2.4). Config: Payment:Momo:{PartnerCode, AccessKey, SecretKey, Endpoint,
    /// RedirectUrl, IpnUrl, RequestType}. MoMo calls the IpnUrl sent with each request, so no portal setup is needed.
    /// </summary>
    public class MomoGateway : ITransientDependency
    {
        public const string DefaultEndpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        public const int MaxOrderIdLength = 50;
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private readonly IConfiguration _configuration;
        private readonly ILogger<MomoGateway> _logger;

        public MomoGateway(IConfiguration configuration, ILogger<MomoGateway> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string? Setting(string name) => _configuration[$"Payment:Momo:{name}"];

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Setting("PartnerCode")) && !string.IsNullOrWhiteSpace(Setting("AccessKey")) &&
            !string.IsNullOrWhiteSpace(Setting("SecretKey")) && !string.IsNullOrWhiteSpace(Setting("RedirectUrl")) &&
            !string.IsNullOrWhiteSpace(Setting("IpnUrl"));

        /// <summary>Creates the MoMo payment and returns its payUrl, or the reason it failed.</summary>
        public async Task<(string? PayUrl, string? Error)> CreatePayUrlAsync(string reference, decimal amount, string orderInfo)
        {
            if (!IsConfigured)
                return (null, "Chưa cấu hình đầy đủ thông tin kết nối MoMo.");

            var request = new MomoCreateRequest
            {
                PartnerCode = Setting("PartnerCode")!,
                RequestId = reference,
                OrderId = reference,
                Amount = decimal.ToInt64(decimal.Round(amount, 0, MidpointRounding.AwayFromZero)),
                OrderInfo = orderInfo,
                RedirectUrl = Setting("RedirectUrl")!,
                IpnUrl = Setting("IpnUrl")!,
                RequestType = Setting("RequestType") is { Length: > 0 } type ? type : "payWithMethod",
                ExtraData = ""
            };
            request.Signature = Sign(
                $"accessKey={Setting("AccessKey")}&amount={request.Amount}&extraData={request.ExtraData}" +
                $"&ipnUrl={request.IpnUrl}&orderId={request.OrderId}&orderInfo={request.OrderInfo}" +
                $"&partnerCode={request.PartnerCode}&redirectUrl={request.RedirectUrl}" +
                $"&requestId={request.RequestId}&requestType={request.RequestType}");

            try
            {
                using var response = await Http.PostAsJsonAsync(Setting("Endpoint") ?? DefaultEndpoint, request);
                var body = await response.Content.ReadFromJsonAsync<MomoCreateResponse>();
                if (body is { ResultCode: 0 } && !string.IsNullOrWhiteSpace(body.PayUrl))
                    return (body.PayUrl, null);
                _logger.LogWarning("MoMo create payment {Reference} failed: {Code} {Message}", reference, body?.ResultCode, body?.Message);
                return (null, $"MoMo từ chối tạo giao dịch: {body?.Message ?? response.StatusCode.ToString()}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "MoMo create payment {Reference} failed", reference);
                return (null, "Không kết nối được cổng thanh toán MoMo. Vui lòng thử lại.");
            }
        }

        /// <summary>Checks the IPN comes from MoMo for this partner (HMAC SHA256 over the documented field order).</summary>
        public bool VerifyIpn(MomoIpnCommand ipn)
        {
            var secret = Setting("SecretKey");
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(ipn.Signature) ||
                !string.Equals(ipn.PartnerCode, Setting("PartnerCode"), StringComparison.Ordinal))
                return false;
            var raw =
                $"accessKey={Setting("AccessKey")}&amount={ipn.Amount.ToString(CultureInfo.InvariantCulture)}&extraData={ipn.ExtraData}" +
                $"&message={ipn.Message}&orderId={ipn.OrderId}&orderInfo={ipn.OrderInfo}&orderType={ipn.OrderType}" +
                $"&partnerCode={ipn.PartnerCode}&payType={ipn.PayType}&requestId={ipn.RequestId}" +
                $"&responseTime={ipn.ResponseTime.ToString(CultureInfo.InvariantCulture)}&resultCode={ipn.ResultCode.ToString(CultureInfo.InvariantCulture)}" +
                $"&transId={ipn.TransId.ToString(CultureInfo.InvariantCulture)}";
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(Sign(raw)), Convert.FromHexString(ipn.Signature));
            }
            catch (FormatException) { return false; }
        }

        private string Sign(string raw) =>
            Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Setting("SecretKey")!), Encoding.UTF8.GetBytes(raw)))
                .ToLowerInvariant();

        private sealed class MomoCreateRequest
        {
            [JsonPropertyName("partnerCode")] public string PartnerCode { get; set; } = "";
            [JsonPropertyName("requestId")] public string RequestId { get; set; } = "";
            [JsonPropertyName("amount")] public long Amount { get; set; }
            [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
            [JsonPropertyName("orderInfo")] public string OrderInfo { get; set; } = "";
            [JsonPropertyName("redirectUrl")] public string RedirectUrl { get; set; } = "";
            [JsonPropertyName("ipnUrl")] public string IpnUrl { get; set; } = "";
            [JsonPropertyName("requestType")] public string RequestType { get; set; } = "";
            [JsonPropertyName("extraData")] public string ExtraData { get; set; } = "";
            [JsonPropertyName("autoCapture")] public bool AutoCapture { get; set; } = true;
            [JsonPropertyName("lang")] public string Lang { get; set; } = "vi";
            [JsonPropertyName("signature")] public string Signature { get; set; } = "";
        }

        private sealed class MomoCreateResponse
        {
            [JsonPropertyName("resultCode")] public int ResultCode { get; set; }
            [JsonPropertyName("message")] public string? Message { get; set; }
            [JsonPropertyName("payUrl")] public string? PayUrl { get; set; }
        }
    }
}
