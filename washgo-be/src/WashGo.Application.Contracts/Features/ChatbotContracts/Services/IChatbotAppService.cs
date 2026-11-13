using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.ChatbotContracts.Commands;
using WashGo.Application.Contracts.Features.ChatbotContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.ChatbotContracts.Services;

public interface IChatbotAppService : IApplicationService
{
    Task<BaseResponse<ChatCursorPageDto<ChatConversationDto>>> GetConversationsAsync(string? cursor, int take = 20);
    Task<BaseResponse<ChatCursorPageDto<ChatMessageDto>>> GetMessagesAsync(Guid id, string? cursor, int take = 30);
    Task<BaseResponse<ChatTurnResultDto>> SendAsync(Guid? conversationId, SendChatMessageCommand command);
    Task<BaseResponse<ChatTurnResultDto>> GetRequestAsync(Guid conversationId, Guid requestId);
    Task<BaseResponse<bool>> DeleteAsync(Guid id);
}
