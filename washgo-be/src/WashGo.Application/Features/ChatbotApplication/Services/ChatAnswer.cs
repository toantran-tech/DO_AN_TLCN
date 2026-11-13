using WashGo.Application.Contracts.Features.ChatbotContracts.Results;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public sealed record ChatAnswer(string Answer, List<ChatSourceDto> Sources, List<ChatCardDto> Cards, string Model, int? TotalTokens);
