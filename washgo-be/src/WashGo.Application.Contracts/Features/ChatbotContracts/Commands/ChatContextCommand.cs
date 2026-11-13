using Volo.Abp.Auditing;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Commands;

[DisableAuditing]
public class ChatContextCommand
{
    public Guid? SelectedOrderId { get; set; }
    public ChatLocationCommand? Location { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
}
