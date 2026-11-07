namespace WashGo.Domain.Shared.Enums
{
    /// <summary>Kết quả gửi email thông tin đăng nhập khi tạo tài khoản Shipper.</summary>
    public enum CredentialDeliveryStatus
    {
        /// <summary>Chưa gửi / không rõ (tài khoản tạo trước khi có trường này).</summary>
        Unknown = 0,
        Sent = 1,
        Failed = 2
    }
}
