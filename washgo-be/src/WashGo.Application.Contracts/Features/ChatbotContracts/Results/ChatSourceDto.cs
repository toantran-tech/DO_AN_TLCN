namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatSourceDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateTime RetrievedAt { get; set; }
}
