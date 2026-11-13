using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatTurnResultDto
{
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public Guid MessageId { get; set; }
    public ChatTurnStatus Status { get; set; }
    public string? Answer { get; set; }
    public string? ErrorCode { get; set; }
    public List<ChatSourceDto> Sources { get; set; } = [];
    public List<ChatCardDto> Cards { get; set; } = [];
    public DateTime ServerNow { get; set; }
}
