using Volo.Abp.Auditing;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Commands;

[DisableAuditing]
public class SendChatMessageCommand
{
    public Guid RequestId { get; set; }
    public string Message { get; set; } = string.Empty;
    public ChatContextCommand? Context { get; set; }
}
