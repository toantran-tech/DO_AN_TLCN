using System.Text.Json.Nodes;
using WashGo.Application.Features.ChatbotApplication.Services;

public class FixtureChatModelClient : IChatModelClient
{
    private static int _calls;
    public static int Calls => _calls;
    public async Task<ChatModelReply> GenerateAsync(JsonArray contents, bool selectTool, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        var text = contents.OfType<JsonObject>().LastOrDefault(x => x["parts"]?[0]?["text"] != null)?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        if (selectTool && text.Contains("CONTEXT_BUDGET_FIXTURE") &&
            (!contents.ToJsonString().Contains("LATEST_BUDGET") || contents.ToJsonString().Contains("OLD_BUDGET")))
            throw new ChatbotException("ContextBudgetFailure");
        if (text.Contains("FAIL_FIXTURE")) throw new ChatbotException("AiUnavailable");
        if (text.Contains("DELAY_FIXTURE")) await Task.Delay(1800, cancellationToken);
        if (!selectTool)
        {
            if (!contents.OfType<JsonObject>().Any(x => x["parts"]?[0]?["thoughtSignature"]?.GetValue<string>() == "fixture-signature"))
                throw new ChatbotException("LostThoughtSignature");
            return new(new JsonObject { ["role"] = "model", ["parts"] = new JsonArray(new JsonObject { ["text"] = "Phản hồi kiểm thử. Xem dữ liệu trong thẻ do server cung cấp." }) }, 20);
        }
        var name = text.Contains("SERVICES_FIXTURE") ? "services" : text.Contains("LOCKERS_FIXTURE") ? "lockers" :
            text.Contains("ORDER_FIXTURE") ? "order" : text.Contains("POLICY_FIXTURE") ? "policy" :
            text.Contains("BAD_TOOL_FIXTURE") ? "mark_paid" : "guide";
        return new(new JsonObject { ["role"] = "model", ["parts"] = new JsonArray(new JsonObject
        { ["functionCall"] = new JsonObject { ["name"] = name, ["args"] = new JsonObject() }, ["thoughtSignature"] = "fixture-signature" }) }, 10);
    }
}
