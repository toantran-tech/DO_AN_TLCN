namespace WashGo.Application.Features.StorageApplication.Services;
internal sealed class StorageRuleException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
