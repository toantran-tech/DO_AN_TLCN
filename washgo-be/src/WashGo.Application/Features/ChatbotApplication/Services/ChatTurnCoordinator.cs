using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ChatbotContracts.Commands;
using WashGo.Domain.Entities.Chatbot;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public class ChatTurnCoordinator(IChatModelClient model, ChatbotContextBuilder contextBuilder, IRepository<ChatMessage, Guid> messages,
    IRepository<ChatConversation, Guid> conversations, IAsyncQueryableExecuter query, IUnitOfWorkManager uows, ChatbotOptions options) : ITransientDependency
{
    public async Task<ChatAnswer> AnswerAsync(Guid owner, Guid conversationId, string text, ChatContextCommand? context, CancellationToken ct)
    {
        if (!options.Configured) throw new ChatbotException("AiNotConfigured");
        var contents = new JsonArray();
        using (var uow = uows.Begin(requiresNew: true, isTransactional: false))
        {
            if (!await conversations.AnyAsync(x => x.Id == conversationId && x.CustomerId == owner && !x.IsDeleted))
                throw new ChatbotException("ConversationNotFound");
            var cutoff = DateTime.UtcNow.AddDays(-options.RetentionDays);
            // Only completed pairs enter context; a failed question must not look like an answered one.
            var rows = await query.ToListAsync((await messages.GetQueryableAsync()).Where(x => x.ConversationId == conversationId &&
                x.CreatedAt >= cutoff && (x.Role == ChatMessageRole.Assistant || x.Status == ChatTurnStatus.Completed))
                .OrderByDescending(x => x.Sequence).Take(12));
            var budget = 10000;
            var recent = new List<(ChatMessage Message, string Text)>();
            foreach (var message in rows.OrderByDescending(x => x.Sequence))
            {
                var content = message.Content[..Math.Min(message.Content.Length, Math.Min(budget, 2000))];
                if (content.Length == 0) break;
                recent.Add((message, content));
                budget -= content.Length;
            }
            foreach (var item in recent.OrderBy(x => x.Message.Sequence))
                contents.Add(Content(item.Message.Role == ChatMessageRole.Assistant ? "model" : "user", item.Text));
            await uow.CompleteAsync();
        }
        contents.Add(Content("user", text + (context?.SelectedOrderId != null ? "\n[Khách đã chọn một đơn trong giao diện.]" : "") +
            (context?.Location != null ? "\n[Khách đã cấp tọa độ hiện tại cho server để tìm locker.]" : "")));
        var selected = await model.GenerateAsync(contents, true, ct);
        var calls = selected.Content["parts"]?.AsArray().Where(x => x?["functionCall"] != null).ToList();
        if (calls?.Count != 1 || calls[0]?["functionCall"] is not JsonObject call) throw new ChatbotException("InvalidAiToolCall");
        var name = call["name"]?.GetValue<string>() ?? string.Empty;
        if (call["args"] is not JsonObject args || args.ToJsonString().Length > 2000) throw new ChatbotException("InvalidAiToolCall");
        ct.ThrowIfCancellationRequested();
        ChatToolResult result;
        using (var uow = uows.Begin(requiresNew: true, isTransactional: false))
        {
            if (!await conversations.AnyAsync(x => x.Id == conversationId && x.CustomerId == owner && !x.IsDeleted))
                throw new ChatbotException("ConversationNotFound");
            result = await contextBuilder.ExecuteAsync(owner, name, args, context);
            await uow.CompleteAsync();
        }
        contents.Add(selected.Content.DeepClone());
        var functionResponse = new JsonObject { ["name"] = name, ["response"] = JsonNode.Parse(result.Data.GetRawText()) };
        // Preserve the function-call ID for models that supply one, plus original thought signatures in the model Content above.
        if (call["id"] != null) functionResponse["id"] = call["id"]!.DeepClone();
        contents.Add(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["functionResponse"] = functionResponse }) });
        var final = await model.GenerateAsync(contents, false, ct);
        var answer = string.Join("\n", final.Content["parts"]?.AsArray()
            .Where(x => x?["thought"]?.GetValue<bool>() != true && x?["text"] != null)
            .Select(x => x!["text"]!.GetValue<string>()) ?? []);
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 10000 || ChatbotAppService.Sensitive(answer)) throw new ChatbotException("InvalidAiResponse");
        // FE renders plain text, never HTML/markdown. Only server cards may carry navigable identifiers.
        answer = Regex.Replace(answer, @"https?://\S+|<[^>]*>", "").Trim();
        return new(answer, result.Sources, result.Cards, options.Model,
            selected.TotalTokens.HasValue && final.TotalTokens.HasValue ? selected.TotalTokens + final.TotalTokens : null);
    }

    private static JsonObject Content(string role, string text) =>
        new() { ["role"] = role, ["parts"] = new JsonArray(new JsonObject { ["text"] = text }) };
}
