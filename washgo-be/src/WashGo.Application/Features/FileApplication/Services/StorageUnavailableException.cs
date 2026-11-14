namespace WashGo.Application.Features.FileApplication.Services;

/// <summary>A provider/configuration failure; clients must not receive upstream bodies or secrets.</summary>
public sealed class StorageUnavailableException(string message) : IOException(message);
