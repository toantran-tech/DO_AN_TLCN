using Volo.Abp.Auditing;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Commands;

[DisableAuditing]
public class ChatLocationCommand
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
