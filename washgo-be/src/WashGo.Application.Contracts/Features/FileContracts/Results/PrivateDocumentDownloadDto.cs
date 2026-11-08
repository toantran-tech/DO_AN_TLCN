namespace WashGo.Application.Contracts.Features.FileContracts.Results;

/// <summary>Internal endpoint result; never includes a physical storage path.</summary>
public class PrivateDocumentDownloadDto
{
    public byte[] Bytes { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
