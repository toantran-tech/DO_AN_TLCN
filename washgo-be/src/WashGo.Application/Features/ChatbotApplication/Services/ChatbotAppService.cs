using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ChatbotContracts.Commands;
using WashGo.Application.Contracts.Features.ChatbotContracts.Results;
using WashGo.Application.Contracts.Features.ChatbotContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Chatbot;
using WashGo.Domain.Entities.Chatbot.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ChatbotApplication.Services;

[DisableAuditing]
public class ChatbotAppService(IChatConversationRepository conversations, IRepository<ChatMessage, Guid> messages,
    ChatCursor cursors, ChatbotOptions options, ChatTurnCoordinator coordinator) : WashGoAppService, IChatbotAppService
{
    private DateTime Cutoff => DateTime.UtcNow.AddDays(-options.RetentionDays);
    private Guid Owner() => CurrentUser.Id.HasValue && CurrentUser.IsInRole("Customer")
        ? CurrentUser.Id.Value : throw new AbpAuthorizationException();
    private static BaseResponse<T> Error<T>(HttpStatusCode status, string code, string message, T? data = default) =>
        new() { Succeeded = false, StatusCode = status, Message = message, Meta = new { code }, Data = data };
    private static BaseResponse<T> Missing<T>() => Error<T>(HttpStatusCode.NotFound, "ConversationNotFound", "Không tìm thấy hội thoại của bạn.");

    public async Task<BaseResponse<ChatCursorPageDto<ChatConversationDto>>> GetConversationsAsync(string? cursor, int take = 20)
    {
        var owner = Owner();
        if (take is < 1 or > 50) return Error<ChatCursorPageDto<ChatConversationDto>>(HttpStatusCode.BadRequest, "InvalidPageSize", "Take từ 1 đến 50.");
        var p = cursor == null ? null : cursors.Decode(cursor, owner, "conversations");
        if (cursor != null && p == null) return Error<ChatCursorPageDto<ChatConversationDto>>(HttpStatusCode.BadRequest, "InvalidCursor", "Cursor không hợp lệ hoặc hết hạn; vui lòng tải lại.");
        var cutoff = Cutoff;
        var q = (await conversations.GetQueryableAsync()).Where(x => x.CustomerId == owner && !x.IsDeleted && x.LastMessageAt >= cutoff);
        var snapshot = p?.Snapshot ?? (await AsyncExecuter.MaxAsync(q.Select(x => (long?)x.Sequence)) ?? 0);
        q = q.Where(x => x.Sequence <= snapshot);
        if (p != null) q = q.Where(x => x.Sequence < p.Before);
        var rows = await AsyncExecuter.ToListAsync(q.OrderByDescending(x => x.Sequence).Take(take + 1));
        var more = rows.Count > take; rows = rows.Take(take).ToList();
        return Success(new ChatCursorPageDto<ChatConversationDto>
        {
            Items = rows.Select(x => new ChatConversationDto { Id = x.Id, Title = x.Title, CreatedAt = x.CreatedAt, LastMessageAt = x.LastMessageAt }).ToList(),
            HasMore = more, NextCursor = more ? cursors.Encode(owner, "conversations", snapshot, rows[^1].Sequence) : null
        });
    }

    public async Task<BaseResponse<ChatCursorPageDto<ChatMessageDto>>> GetMessagesAsync(Guid id, string? cursor, int take = 30)
    {
        var owner = Owner(); var cutoff = Cutoff;
        if (!await conversations.AnyAsync(x => x.Id == id && x.CustomerId == owner && !x.IsDeleted && x.LastMessageAt >= cutoff)) return Missing<ChatCursorPageDto<ChatMessageDto>>();
        if (take is < 1 or > 100) return Error<ChatCursorPageDto<ChatMessageDto>>(HttpStatusCode.BadRequest, "InvalidPageSize", "Take từ 1 đến 100.");
        var resource = $"messages:{id}";
        var p = cursor == null ? null : cursors.Decode(cursor, owner, resource);
        if (cursor != null && p == null) return Error<ChatCursorPageDto<ChatMessageDto>>(HttpStatusCode.BadRequest, "InvalidCursor", "Cursor không hợp lệ hoặc hết hạn; vui lòng tải lại.");
        var cq = await conversations.GetQueryableAsync();
        var q = (await messages.GetQueryableAsync()).Where(x => x.ConversationId == id && !x.IsDeleted && x.CreatedAt >= cutoff &&
            cq.Any(c => c.Id == x.ConversationId && c.CustomerId == owner && !c.IsDeleted));
        var snapshot = p?.Snapshot ?? (await AsyncExecuter.MaxAsync(q.Select(x => (long?)x.Sequence)) ?? 0);
        q = q.Where(x => x.Sequence <= snapshot);
        if (p != null) q = q.Where(x => x.Sequence < p.Before);
        var rows = await AsyncExecuter.ToListAsync(q.OrderByDescending(x => x.Sequence).Take(take + 1));
        var more = rows.Count > take; rows = rows.Take(take).ToList();
        var next = more ? cursors.Encode(owner, resource, snapshot, rows[^1].Sequence) : null;
        return Success(new ChatCursorPageDto<ChatMessageDto>
        {
            Items = rows.OrderBy(x => x.Sequence).Select(x => new ChatMessageDto
            {
                Id = x.Id, Sequence = x.Sequence, Role = x.Role, Content = x.Content, CreatedAt = x.CreatedAt,
                RequestId = x.RequestId, Status = x.Status, ErrorCode = x.ErrorCode, Result = ChatbotJson.Read<ChatTurnResultDto>(x.ResultJson)
            }).ToList(), HasMore = more, NextCursor = next
        });
    }

    [UnitOfWork(IsDisabled = true)]
    public virtual async Task<BaseResponse<ChatTurnResultDto>> SendAsync(Guid? conversationId, SendChatMessageCommand command)
    {
        var owner = Owner();
        var text = command.Message?.Trim() ?? string.Empty;
        if (command.RequestId == Guid.Empty || text.Length is < 1 or > 2000 || text.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
            return Error<ChatTurnResultDto>(HttpStatusCode.BadRequest, "InvalidChatMessage", "Cần requestId hợp lệ và câu hỏi 1–2.000 ký tự.");
        if (Sensitive(text)) return Error<ChatTurnResultDto>(HttpStatusCode.BadRequest, "SensitiveChatInput", "Không gửi OTP, mã mở tủ, mật khẩu hoặc token vào hội thoại.");
        if (ChatbotContextBuilder.Validate(command.Context) is { } invalid)
            return Error<ChatTurnResultDto>(HttpStatusCode.BadRequest, "InvalidChatContext", invalid);
        var fingerprint = cursors.Fingerprint(ChatbotJson.Serialize(new { conversationId, text, command.Context }));
        // Deterministic ID makes a repeated first-message request resolve to the same conversation, including after deletion.
        var id = conversationId ?? new Guid(Convert.FromHexString(cursors.Fingerprint($"first:{owner}:{command.RequestId}"))[..16]);
        Guid messageId;
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            await conversations.LockCustomerAsync(owner);
            var conversation = await conversations.FindAsync(id);
            if (conversation != null && (conversation.CustomerId != owner || conversation.IsDeleted || conversation.LastMessageAt < Cutoff)) return Missing<ChatTurnResultDto>();
            if (conversation == null && conversationId.HasValue) return Missing<ChatTurnResultDto>();
            if (conversation != null)
            {
                await ExpireLeaseAsync(conversation);
                var old = await messages.FirstOrDefaultAsync(x => x.ConversationId == id && x.RequestId == command.RequestId);
                if (old != null)
                {
                    if (old.Fingerprint != fingerprint) return Error<ChatTurnResultDto>(HttpStatusCode.Conflict, "RequestIdReused", "RequestId đã dùng với nội dung khác.");
                    await uow.CompleteAsync();
                    return TurnResponse(Turn(old));
                }
                if (conversation.ActiveMessageId.HasValue) return Error<ChatTurnResultDto>(HttpStatusCode.Conflict, "ConversationBusy", "Hội thoại đang xử lý câu hỏi trước.");
            }
            var now = DateTime.UtcNow;
            var dayStart = now.AddHours(7).Date.AddHours(-7);
            var cq = await conversations.GetQueryableAsync();
            var mine = (await messages.GetQueryableAsync()).Where(x => x.Role == ChatMessageRole.Customer && cq.Any(c => c.Id == x.ConversationId && c.CustomerId == owner));
            var minuteStart = now.AddMinutes(-1);
            if (await AsyncExecuter.CountAsync(mine.Where(x => x.CreatedAt >= minuteStart)) >= options.PerMinute ||
                await AsyncExecuter.CountAsync(mine.Where(x => x.CreatedAt >= dayStart)) >= options.PerDay)
                return Error<ChatTurnResultDto>(HttpStatusCode.TooManyRequests, "ChatRateLimited", "Đã đạt giới hạn chat. Vui lòng thử lại sau.");
            if (await conversations.CountAsync(x => x.CustomerId == owner && x.ActiveMessageId != null && x.LeaseUntil > now && !x.IsDeleted) >= 2)
                return Error<ChatTurnResultDto>(HttpStatusCode.Conflict, "CustomerChatBusy", "Bạn đang có hai câu hỏi được xử lý.");
            if (conversation == null)
            {
                conversation = new ChatConversation(id) { CustomerId = owner, Title = text[..Math.Min(100, text.Length)], CreatedAt = now, LastMessageAt = now };
                await conversations.InsertAsync(conversation, autoSave: true);
            }
            var message = new ChatMessage(GuidGenerator.Create()) { ConversationId = id, Content = text, Role = ChatMessageRole.Customer,
                CreatedAt = now, RequestId = command.RequestId, Fingerprint = fingerprint, Status = ChatTurnStatus.Processing };
            await messages.InsertAsync(message, autoSave: true);
            messageId = message.Id;
            conversation.ActiveMessageId = messageId;
            conversation.LeaseUntil = now.AddSeconds(options.TimeoutSeconds + 20);
            conversation.LastMessageAt = now;
            await conversations.UpdateAsync(conversation);
            await uow.CompleteAsync();
        }

        // No DB transaction spans Gemini or its read tools. The coordinator uses independent short read scopes.
        ChatTurnResultDto result;
        string? model = null; int? tokens = null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            var generated = await coordinator.AnswerAsync(owner, id, text, command.Context, timeout.Token);
            model = generated.Model; tokens = generated.TotalTokens;
            result = new() { Answer = generated.Answer, Sources = generated.Sources, Cards = generated.Cards, Status = ChatTurnStatus.Completed };
        }
        catch (ChatbotException e) { result = new() { Status = ChatTurnStatus.Failed, ErrorCode = e.Code }; }
        catch (OperationCanceledException) { result = new() { Status = ChatTurnStatus.Failed, ErrorCode = "AiTimeout" }; }
        catch (Exception) { result = new() { Status = ChatTurnStatus.Failed, ErrorCode = "AiUnavailable" }; }

        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
        {
            await conversations.LockCustomerAsync(owner);
            var conversation = await conversations.FindAsync(id);
            if (conversation == null || conversation.IsDeleted || conversation.CustomerId != owner) return Missing<ChatTurnResultDto>();
            var message = await messages.FindAsync(messageId);
            if (message == null) return Missing<ChatTurnResultDto>();
            await ExpireLeaseAsync(conversation);
            if (conversation.ActiveMessageId != messageId || message.Status != ChatTurnStatus.Processing)
            {
                await uow.CompleteAsync();
                return TurnResponse(Turn(message));
            }
            result.ConversationId = id; result.MessageId = messageId; result.RequestId = command.RequestId; result.ServerNow = DateTime.UtcNow;
            message.Status = result.Status; message.ErrorCode = result.ErrorCode; message.Model = model; message.TotalTokens = tokens;
            message.ResultJson = ChatbotJson.Serialize(result);
            await messages.UpdateAsync(message);
            if (result.Status == ChatTurnStatus.Completed)
                await messages.InsertAsync(new ChatMessage(GuidGenerator.Create()) { ConversationId = id, Role = ChatMessageRole.Assistant,
                    Content = result.Answer!, CreatedAt = DateTime.UtcNow, ReplyToMessageId = messageId, ResultJson = message.ResultJson });
            conversation.ActiveMessageId = null; conversation.LeaseUntil = null; conversation.LastMessageAt = DateTime.UtcNow;
            await conversations.UpdateAsync(conversation);
            await uow.CompleteAsync();
        }
        return TurnResponse(result);
    }

    public async Task<BaseResponse<ChatTurnResultDto>> GetRequestAsync(Guid conversationId, Guid requestId)
    {
        var owner = Owner();
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await conversations.LockCustomerAsync(owner);
        var conversation = await conversations.FindAsync(conversationId);
        if (conversation == null || conversation.CustomerId != owner || conversation.IsDeleted || conversation.LastMessageAt < Cutoff) return Missing<ChatTurnResultDto>();
        await ExpireLeaseAsync(conversation);
        var cutoff = Cutoff;
        var message = await messages.FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.RequestId == requestId && x.CreatedAt >= cutoff);
        if (message == null) return Error<ChatTurnResultDto>(HttpStatusCode.NotFound, "ChatRequestNotFound", "Chưa tìm thấy lượt gửi; không đồng nghĩa request trước chưa được xử lý.");
        await uow.CompleteAsync();
        return Success(Turn(message));
    }

    public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
    {
        var owner = Owner();
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await conversations.LockCustomerAsync(owner);
        var conversation = await conversations.FindAsync(id);
        if (conversation == null || conversation.CustomerId != owner) return Missing<bool>();
        conversation.IsDeleted = true; conversation.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        conversation.ActiveMessageId = null; conversation.LeaseUntil = null;
        await conversations.UpdateAsync(conversation);
        await uow.CompleteAsync();
        return Success(true);
    }

    private async Task ExpireLeaseAsync(ChatConversation conversation)
    {
        if (conversation.ActiveMessageId is not { } active || conversation.LeaseUntil > DateTime.UtcNow) return;
        var message = await messages.FindAsync(active);
        if (message != null && message.Status == ChatTurnStatus.Processing)
        {
            message.Status = ChatTurnStatus.Failed; message.ErrorCode = "ChatTurnExpired";
            await messages.UpdateAsync(message);
        }
        conversation.ActiveMessageId = null; conversation.LeaseUntil = null;
        await conversations.UpdateAsync(conversation);
    }

    private static ChatTurnResultDto Turn(ChatMessage message)
    {
        var saved = ChatbotJson.Read<ChatTurnResultDto>(message.ResultJson);
        return saved ?? new() { ConversationId = message.ConversationId, RequestId = message.RequestId!.Value, MessageId = message.Id,
            Status = message.Status!.Value, ErrorCode = message.ErrorCode, ServerNow = DateTime.UtcNow };
    }
    private BaseResponse<ChatTurnResultDto> TurnResponse(ChatTurnResultDto result) => result.Status switch
    {
        ChatTurnStatus.Completed => Success(result),
        ChatTurnStatus.Processing => new() { Succeeded = true, StatusCode = HttpStatusCode.Accepted, Data = result, Message = "Đang xử lý; tra cứu lại bằng requestId." },
        _ => Error(HttpStatusCode.ServiceUnavailable, result.ErrorCode ?? "AiUnavailable", "Chưa thể trả lời lúc này. Xem trạng thái lượt gửi trước khi thử câu hỏi mới.", result)
    };

    internal static bool Sensitive(string text) => Regex.IsMatch(text,
        @"(?i)WASHGO:(DEPOSIT|PICKUP|COLLECT|RETURN):|eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]+\.|(?:otp|pin|mật khẩu|password|api.?key|secret|refresh.?token)\s*[:=]\s*\S+");
}
