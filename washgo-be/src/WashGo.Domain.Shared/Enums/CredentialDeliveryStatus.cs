namespace WashGo.Domain.Shared.Enums
{
    /// <summary>Kết quả gửi email thông tin đăng nhập khi tạo tài khoản.</summary>
    public enum CredentialDeliveryStatus
    {
        /// <summary>Chưa gửi/đang gửi/không rõ; không được tự suy là thất bại để gửi lại.</summary>
        Unknown = 0,
        /// <summary>SMTP đã chấp nhận yêu cầu gửi, không bảo đảm thư vào inbox.</summary>
        Sent = 1,
        Failed = 2
    }
}
