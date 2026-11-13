using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using System.Text.RegularExpressions;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public class ChatbotOptions(IConfiguration configuration) : ITransientDependency
{
    public string ApiKey => configuration["Chatbot:Gemini:ApiKey"] ?? string.Empty;
    public string Model => configuration["Chatbot:Gemini:Model"] ?? "gemini-3.5-flash-lite";
    public string SigningKey => configuration["Chatbot:SigningKey"] ?? configuration["Jwt:SecretKey"] ?? string.Empty;
    public bool Configured => ApiKey.Length > 0 && Regex.IsMatch(Model, @"^gemini-[a-z0-9.-]{1,80}$");
    public int PerMinute => Limit("PerMinute", 10, 1, 60);
    public int PerDay => Limit("PerDay", 100, 1, 1000);
    public int TimeoutSeconds => Limit("TimeoutSeconds", 30, 5, 60);
    public int RetentionDays => 90;
    private int Limit(string name, int fallback, int min, int max) =>
        Math.Clamp(configuration.GetValue<int?>("Chatbot:" + name) ?? fallback, min, max);
}
