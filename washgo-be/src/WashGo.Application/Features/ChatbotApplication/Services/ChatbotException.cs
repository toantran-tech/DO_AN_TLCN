namespace WashGo.Application.Features.ChatbotApplication.Services;

public class ChatbotException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
