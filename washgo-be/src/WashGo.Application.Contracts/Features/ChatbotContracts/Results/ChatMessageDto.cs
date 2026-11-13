using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? RequestId { get; set; }
    public ChatTurnStatus? Status { get; set; }
    public string? ErrorCode { get; set; }
    public ChatTurnResultDto? Result { get; set; }
}
