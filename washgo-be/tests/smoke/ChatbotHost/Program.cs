using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WashGo.Application.Features.ChatbotApplication.Services;
using WashGo.HttpApi.Host;
using Volo.Abp.Uow;
using WashGo.Domain.Entities.Chatbot.Interfaces;

// Deliberately a separate executable: the production host has no mock provider switch.
if (Environment.GetEnvironmentVariable("WASHGO_CHATBOT_SMOKE") != "1")
    throw new InvalidOperationException("Set WASHGO_CHATBOT_SMOKE=1; local integration test only.");
await GeminiContractChecks.RunAsync();
var sourceRoot = new DirectoryInfo(AppContext.BaseDirectory);
while (sourceRoot != null && !File.Exists(Path.Combine(sourceRoot.FullName, "WashGo.HttpApi.Host", "appsettings.json"))) sourceRoot = sourceRoot.Parent;
if (sourceRoot == null) throw new InvalidOperationException("Run fixture host from the source checkout.");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args, ContentRootPath = Path.Combine(sourceRoot.FullName, "WashGo.HttpApi.Host"), EnvironmentName = "Development"
});
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Chatbot:Gemini:ApiKey"] = "fixture-not-a-real-key", ["Chatbot:Gemini:Model"] = "gemini-3.5-flash-lite",
    ["Chatbot:PerMinute"] = "60", ["Chatbot:PerDay"] = "1000", ["Email:Smtp:Host"] = "",
    ["Logging:LogLevel:Default"] = "Error"
});
builder.Host.UseAutofac();
await builder.AddApplicationAsync<WashGoHttpApiHostModule>();
builder.Services.RemoveAll<IHostedService>();
builder.Services.Replace(ServiceDescriptor.Singleton<IChatModelClient, FixtureChatModelClient>());
var app = builder.Build();
await app.InitializeApplicationAsync();
app.MapGet("/fixture/calls", () => new { calls = FixtureChatModelClient.Calls });
app.MapPost("/fixture/purge", async (IChatConversationRepository repository, IUnitOfWorkManager uows) =>
{
    using var uow = uows.Begin(requiresNew: true, isTransactional: true);
    await repository.PurgeAsync(DateTime.UtcNow.AddDays(-90));
    await uow.CompleteAsync();
    return new { purged = true };
});
await app.RunAsync("http://localhost:5099");
