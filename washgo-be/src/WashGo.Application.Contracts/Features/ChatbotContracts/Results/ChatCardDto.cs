using System.Text.Json;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Results;

public class ChatCardDto
{
    /// <summary>Service / Locker / Order / Policy. FE maps this type and ID to its own allowed route.</summary>
    public string Type { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
}
