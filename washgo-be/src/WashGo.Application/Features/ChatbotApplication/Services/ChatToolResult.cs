using System.Text.Json;
using WashGo.Application.Contracts.Features.ChatbotContracts.Results;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public sealed record ChatToolResult(JsonElement Data, List<ChatSourceDto> Sources, List<ChatCardDto> Cards);
