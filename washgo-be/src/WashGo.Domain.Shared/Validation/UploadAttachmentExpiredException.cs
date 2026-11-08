namespace WashGo.Domain.Shared.Validation;

public class UploadAttachmentExpiredException : Exception
{
    public UploadAttachmentExpiredException() : base("Ảnh/video bản nháp đã hết hạn hoặc bị dọn; vui lòng upload lại.") { }
}
