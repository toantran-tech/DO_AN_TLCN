namespace WashGo.Application.Features.StorageApplication.Services;
internal sealed class StorageRuleException(string code, string message, int status = 400, object? meta = null) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    /// <summary>Full response meta (must contain code) when the error carries extra numbers, e.g. ShipperCapacityExceeded.</summary>
    public object? Meta { get; } = meta;
}
