using System.Text.Json.Nodes;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public interface IChatModelClient
{
    Task<ChatModelReply> GenerateAsync(JsonArray contents, bool selectTool, CancellationToken cancellationToken);
}
