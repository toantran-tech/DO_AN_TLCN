namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatCursorPageDto<T>
{
    public List<T> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}
