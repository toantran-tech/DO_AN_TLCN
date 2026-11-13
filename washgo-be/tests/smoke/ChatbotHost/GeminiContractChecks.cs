using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using WashGo.Application.Features.ChatbotApplication.Providers;
using WashGo.Application.Features.ChatbotApplication.Services;

public static class GeminiContractChecks
{
    public static async Task RunAsync()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Chatbot:Gemini:ApiKey"] = "contract-test-key", ["Chatbot:Gemini:Model"] = "gemini-3.5-flash-lite",
            ["Jwt:SecretKey"] = "Contract-test-signing-key-at-least-32-characters"
        }).Build();
        var options = new ChatbotOptions(config);
        var cursor = new ChatCursor(options); var owner = Guid.NewGuid();
        var value = cursor.Encode(owner, "messages:test", 100, 50);
        if (cursor.Decode(value, owner, "messages:test")?.Before != 50 || cursor.Decode(value, Guid.NewGuid(), "messages:test") != null ||
            cursor.Decode(value + "0", owner, "messages:test") != null || cursor.Decode(value, owner, "conversations") != null)
            throw new Exception("Cursor owner/resource/signature checks failed.");
        foreach (var status in new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway })
        {
            using var handler = new ContractHandler(status);
            var client = new GeminiChatModelClient(new HttpClient(handler), options);
            try
            {
                var answer = await client.GenerateAsync(new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = "test" }) }), false, CancellationToken.None);
                if (status != HttpStatusCode.OK || answer.TotalTokens != 17 || answer.Content["parts"]?[0]?["text"]?.GetValue<string>() != "contract reply") throw new Exception("Unexpected Gemini parser result.");
            }
            catch (ChatbotException error)
            {
                var expected = status == HttpStatusCode.Forbidden ? "AiConfigurationRejected" : status == HttpStatusCode.TooManyRequests ? "AiRateLimited" : "AiUnavailable";
                if (status == HttpStatusCode.OK || error.Code != expected) throw;
            }
        }
        Console.WriteLine("PASS: Gemini HTTP header/body/parser/error contract and signed cursors (no network).");
    }

    private sealed class ContractHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host != "generativelanguage.googleapis.com" || request.RequestUri.Query.Length != 0 ||
                !request.Headers.TryGetValues("x-goog-api-key", out var key) || key.Single() != "contract-test-key") throw new Exception("Gemini auth leaked into URL or target is wrong.");
            var json = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            if (json["systemInstruction"] == null || json["tools"] == null || json["toolConfig"]?["functionCallingConfig"]?["mode"]?.GetValue<string>() != "NONE") throw new Exception("Gemini body shape is wrong.");
            return new(status) { Content = new StringContent("""{"candidates":[{"finishReason":"STOP","content":{"role":"model","parts":[{"text":"contract reply"}]}}],"usageMetadata":{"totalTokenCount":17}}""", Encoding.UTF8, "application/json") };
        }
    }
}
