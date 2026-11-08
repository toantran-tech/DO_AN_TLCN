using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.FileContracts.Results;

public class PrivateDocumentDto
{
    public Guid FileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public MerchantDocumentKind Kind { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string DownloadUrl => $"/files/documents/{FileId}/download";
    public bool PreviewSupported => ContentType == "application/pdf";
}
