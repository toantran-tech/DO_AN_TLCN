using System.Text.Json.Nodes;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public sealed record ChatModelReply(JsonObject Content, int? TotalTokens);
