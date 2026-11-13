using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ChatbotContracts.Commands;
using WashGo.Application.Contracts.Features.ChatbotContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Chatbot;

public class ChatbotHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customer/chatbot").RequireAuthorization("CustomerOnly").WithTags("Customer - Trợ lý AI");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/conversations", async (string? cursor, int? take, IChatbotAppService service) =>
            (await service.GetConversationsAsync(cursor, take ?? 20)).CustomResult())
            .WithSummary("Lịch sử chat của tôi: lazy load bằng cursor, mặc định 20, tối đa 50. Thứ tự tạo mới nhất trước.").WithOpenApi();
        group.MapPost("/conversations/first-message", async (SendChatMessageCommand command, IChatbotAppService service) =>
            (await service.SendAsync(null, command)).CustomResult())
            .WithSummary("Gửi tin đầu để tạo hội thoại. RequestId giữ nguyên khi retry; không tạo hội thoại rỗng.").WithOpenApi();
        group.MapGet("/conversations/{id:guid}/messages", async (Guid id, string? cursor, int? take, IChatbotAppService service) =>
            (await service.GetMessagesAsync(id, cursor, take ?? 30)).CustomResult())
            .WithSummary("Tải tin gần nhất; cuộn lên dùng nextCursor tải tin cũ. Mỗi đợt trả cũ đến mới, mặc định 30, tối đa 100.").WithOpenApi();
        group.MapPost("/conversations/{id:guid}/messages", async (Guid id, SendChatMessageCommand command, IChatbotAppService service) =>
            (await service.SendAsync(id, command)).CustomResult())
            .WithSummary("Hỏi tiếp Gemini; 2.000 ký tự, không gửi OTP/PIN/password. 202 là đang xử lý; lookup trước khi thử lại.").WithOpenApi();
        group.MapGet("/conversations/{id:guid}/requests/{requestId:guid}", async (Guid id, Guid requestId, IChatbotAppService service) =>
            (await service.GetRequestAsync(id, requestId)).CustomResult())
            .WithSummary("Tra trạng thái và câu trả lời bằng requestId sau timeout. Chỉ hội thoại của tôi.").WithOpenApi();
        group.MapDelete("/conversations/{id:guid}", async (Guid id, IChatbotAppService service) =>
            (await service.DeleteAsync(id)).CustomResult())
            .WithSummary("Xóa hội thoại khỏi lịch sử; không cho lượt AI đang chạy ghi sống lại. Dữ liệu hết hạn được dọn nền sau 90 ngày.").WithOpenApi();
    }
}
