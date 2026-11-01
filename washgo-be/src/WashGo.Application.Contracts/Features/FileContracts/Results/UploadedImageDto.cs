namespace WashGo.Application.Contracts.Features.FileContracts.Results
{
    public class UploadedImageDto
    {
        /// <summary>Đường dẫn tương đối dạng /uploads/{userId}/{file}; gửi lại trong imageUrls của đánh giá/khiếu nại.</summary>
        public string Url { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }
}
