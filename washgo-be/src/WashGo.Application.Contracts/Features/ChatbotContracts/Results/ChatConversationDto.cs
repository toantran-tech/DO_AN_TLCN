namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}
